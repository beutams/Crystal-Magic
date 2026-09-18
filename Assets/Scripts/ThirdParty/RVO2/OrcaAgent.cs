/*
 * Derived from RVO2 Library C# Agent.cs and Vector2.cs.
 * SPDX-FileCopyrightText: 2008 University of North Carolina at Chapel Hill
 * SPDX-License-Identifier: Apache-2.0
 *
 * Modified for Crystal Magic: the internal KD-tree, static-obstacle solver,
 * parallel simulation loop, and position integration were removed. Agent
 * neighbors are supplied by the game's UnitQueryGrid and ECS remains the
 * authoritative owner of positions and velocities.
 */

using System;
using System.Collections.Generic;

namespace CrystalMagic.ThirdParty.RVO2
{
    internal sealed class Agent
    {
        private readonly List<KeyValuePair<float, Agent>> _agentNeighbors = new();
        private readonly List<Line> _orcaLines = new();
        private Vector2 _newVelocity;

        internal int Id { get; private set; }
        internal int MaxNeighbors { get; private set; }
        internal float MaxSpeed { get; private set; }
        internal float NeighborDistance { get; private set; }
        internal float Radius { get; private set; }
        internal float TimeHorizon { get; private set; }
        internal Vector2 Position { get; private set; }
        internal Vector2 PreferredVelocity { get; private set; }
        internal Vector2 Velocity { get; private set; }
        internal Vector2 NewVelocity => _newVelocity;

        internal void Configure(
            int id,
            Vector2 position,
            Vector2 velocity,
            Vector2 preferredVelocity,
            float neighborDistance,
            int maxNeighbors,
            float timeHorizon,
            float radius,
            float maxSpeed)
        {
            Id = id;
            Position = position;
            Velocity = velocity;
            PreferredVelocity = preferredVelocity;
            NeighborDistance = Math.Max(0f, neighborDistance);
            MaxNeighbors = Math.Max(0, maxNeighbors);
            TimeHorizon = Math.Max(RvoMath.Epsilon, timeHorizon);
            Radius = Math.Max(0f, radius);
            MaxSpeed = Math.Max(0f, maxSpeed);
            _newVelocity = velocity;
        }

        internal void BeginNeighborQuery()
        {
            _agentNeighbors.Clear();
        }

        internal void InsertAgentNeighbor(Agent agent, ref float rangeSq)
        {
            if (agent == null || agent == this || MaxNeighbors <= 0)
                return;

            float distanceSq = RvoMath.AbsSq(Position - agent.Position);
            if (distanceSq >= rangeSq)
                return;

            if (_agentNeighbors.Count < MaxNeighbors)
                _agentNeighbors.Add(new KeyValuePair<float, Agent>(distanceSq, agent));

            int index = _agentNeighbors.Count - 1;
            while (index != 0 && distanceSq < _agentNeighbors[index - 1].Key)
            {
                _agentNeighbors[index] = _agentNeighbors[index - 1];
                index--;
            }

            _agentNeighbors[index] = new KeyValuePair<float, Agent>(distanceSq, agent);
            if (_agentNeighbors.Count == MaxNeighbors)
                rangeSq = _agentNeighbors[_agentNeighbors.Count - 1].Key;
        }

        internal void ComputeNewVelocity(float timeStep)
        {
            _orcaLines.Clear();
            float inverseTimeHorizon = 1f / TimeHorizon;
            float inverseTimeStep = 1f / Math.Max(RvoMath.Epsilon, timeStep);

            for (int index = 0; index < _agentNeighbors.Count; index++)
            {
                Agent other = _agentNeighbors[index].Value;
                Vector2 relativePosition = other.Position - Position;
                Vector2 relativeVelocity = Velocity - other.Velocity;
                float distanceSq = RvoMath.AbsSq(relativePosition);
                float combinedRadius = Radius + other.Radius;
                float combinedRadiusSq = combinedRadius * combinedRadius;

                Line line;
                Vector2 correction;

                if (distanceSq > combinedRadiusSq)
                {
                    Vector2 w = relativeVelocity - inverseTimeHorizon * relativePosition;
                    float wLengthSq = RvoMath.AbsSq(w);
                    float dotProduct = w * relativePosition;

                    if (dotProduct < 0f && dotProduct * dotProduct > combinedRadiusSq * wLengthSq)
                    {
                        float wLength = RvoMath.Sqrt(wLengthSq);
                        Vector2 unitW = w / wLength;
                        line.Direction = new Vector2(unitW.Y, -unitW.X);
                        correction = (combinedRadius * inverseTimeHorizon - wLength) * unitW;
                    }
                    else
                    {
                        float leg = RvoMath.Sqrt(Math.Max(0f, distanceSq - combinedRadiusSq));
                        if (RvoMath.Det(relativePosition, w) > 0f)
                        {
                            line.Direction = new Vector2(
                                relativePosition.X * leg - relativePosition.Y * combinedRadius,
                                relativePosition.X * combinedRadius + relativePosition.Y * leg) / distanceSq;
                        }
                        else
                        {
                            line.Direction = -new Vector2(
                                relativePosition.X * leg + relativePosition.Y * combinedRadius,
                                -relativePosition.X * combinedRadius + relativePosition.Y * leg) / distanceSq;
                        }

                        float directionDot = relativeVelocity * line.Direction;
                        correction = directionDot * line.Direction - relativeVelocity;
                    }
                }
                else
                {
                    Vector2 w = relativeVelocity - inverseTimeStep * relativePosition;
                    float wLength = RvoMath.Abs(w);
                    Vector2 unitW;
                    if (wLength > RvoMath.Epsilon)
                    {
                        unitW = w / wLength;
                    }
                    else if (distanceSq > RvoMath.Epsilon * RvoMath.Epsilon)
                    {
                        unitW = RvoMath.Normalize(-relativePosition);
                    }
                    else
                    {
                        unitW = Id < other.Id ? new Vector2(1f, 0f) : new Vector2(-1f, 0f);
                    }

                    line.Direction = new Vector2(unitW.Y, -unitW.X);
                    correction = (combinedRadius * inverseTimeStep - wLength) * unitW;
                }

                line.Point = Velocity + 0.5f * correction;
                _orcaLines.Add(line);
            }

            int failedLine = LinearProgram2(_orcaLines, MaxSpeed, PreferredVelocity, false, out _newVelocity);
            if (failedLine < _orcaLines.Count)
                LinearProgram3(_orcaLines, failedLine, MaxSpeed, ref _newVelocity);
        }

        private static bool LinearProgram1(
            IReadOnlyList<Line> lines,
            int lineNumber,
            float radius,
            Vector2 optimalVelocity,
            bool directionOnly,
            ref Vector2 result)
        {
            float dotProduct = lines[lineNumber].Point * lines[lineNumber].Direction;
            float discriminant = dotProduct * dotProduct + radius * radius - RvoMath.AbsSq(lines[lineNumber].Point);
            if (discriminant < 0f)
                return false;

            float sqrtDiscriminant = RvoMath.Sqrt(discriminant);
            float left = -dotProduct - sqrtDiscriminant;
            float right = -dotProduct + sqrtDiscriminant;

            for (int index = 0; index < lineNumber; index++)
            {
                float denominator = RvoMath.Det(lines[lineNumber].Direction, lines[index].Direction);
                float numerator = RvoMath.Det(
                    lines[index].Direction,
                    lines[lineNumber].Point - lines[index].Point);

                if (Math.Abs(denominator) <= RvoMath.Epsilon)
                {
                    if (numerator < 0f)
                        return false;
                    continue;
                }

                float t = numerator / denominator;
                if (denominator >= 0f)
                    right = Math.Min(right, t);
                else
                    left = Math.Max(left, t);

                if (left > right)
                    return false;
            }

            if (directionOnly)
            {
                result = optimalVelocity * lines[lineNumber].Direction > 0f
                    ? lines[lineNumber].Point + right * lines[lineNumber].Direction
                    : lines[lineNumber].Point + left * lines[lineNumber].Direction;
            }
            else
            {
                float t = lines[lineNumber].Direction * (optimalVelocity - lines[lineNumber].Point);
                if (t < left)
                    result = lines[lineNumber].Point + left * lines[lineNumber].Direction;
                else if (t > right)
                    result = lines[lineNumber].Point + right * lines[lineNumber].Direction;
                else
                    result = lines[lineNumber].Point + t * lines[lineNumber].Direction;
            }

            return true;
        }

        private static int LinearProgram2(
            IReadOnlyList<Line> lines,
            float radius,
            Vector2 optimalVelocity,
            bool directionOnly,
            out Vector2 result)
        {
            if (directionOnly)
                result = optimalVelocity * radius;
            else if (RvoMath.AbsSq(optimalVelocity) > radius * radius)
                result = RvoMath.Normalize(optimalVelocity) * radius;
            else
                result = optimalVelocity;

            for (int index = 0; index < lines.Count; index++)
            {
                if (RvoMath.Det(lines[index].Direction, lines[index].Point - result) <= 0f)
                    continue;

                Vector2 previousResult = result;
                if (!LinearProgram1(lines, index, radius, optimalVelocity, directionOnly, ref result))
                {
                    result = previousResult;
                    return index;
                }
            }

            return lines.Count;
        }

        private static void LinearProgram3(
            IReadOnlyList<Line> lines,
            int firstFailedLine,
            float radius,
            ref Vector2 result)
        {
            float distance = 0f;
            for (int index = firstFailedLine; index < lines.Count; index++)
            {
                if (RvoMath.Det(lines[index].Direction, lines[index].Point - result) <= distance)
                    continue;

                List<Line> projectedLines = new(index);
                for (int previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    Line line;
                    float determinant = RvoMath.Det(lines[index].Direction, lines[previousIndex].Direction);
                    if (Math.Abs(determinant) <= RvoMath.Epsilon)
                    {
                        if (lines[index].Direction * lines[previousIndex].Direction > 0f)
                            continue;

                        line.Point = 0.5f * (lines[index].Point + lines[previousIndex].Point);
                    }
                    else
                    {
                        line.Point = lines[index].Point +
                                     RvoMath.Det(
                                         lines[previousIndex].Direction,
                                         lines[index].Point - lines[previousIndex].Point) /
                                     determinant * lines[index].Direction;
                    }

                    line.Direction = RvoMath.Normalize(lines[previousIndex].Direction - lines[index].Direction);
                    projectedLines.Add(line);
                }

                Vector2 previousResult = result;
                Vector2 optimizationDirection = new(-lines[index].Direction.Y, lines[index].Direction.X);
                if (LinearProgram2(projectedLines, radius, optimizationDirection, true, out result) < projectedLines.Count)
                    result = previousResult;

                distance = RvoMath.Det(lines[index].Direction, lines[index].Point - result);
            }
        }
    }

    internal struct Line
    {
        internal Vector2 Direction;
        internal Vector2 Point;
    }

    internal static class RvoMath
    {
        internal const float Epsilon = 0.00001f;

        internal static float Abs(Vector2 vector) => Sqrt(AbsSq(vector));

        internal static float AbsSq(Vector2 vector) => vector * vector;

        internal static float Det(Vector2 first, Vector2 second) =>
            first.X * second.Y - first.Y * second.X;

        internal static Vector2 Normalize(Vector2 vector)
        {
            float length = Abs(vector);
            return length <= Epsilon ? new Vector2(0f, 0f) : vector / length;
        }

        internal static float Sqrt(float value) => (float)Math.Sqrt(value);
    }

    internal readonly struct Vector2
    {
        internal Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        internal float X { get; }
        internal float Y { get; }

        public static float operator *(Vector2 first, Vector2 second) =>
            first.X * second.X + first.Y * second.Y;

        public static Vector2 operator *(float scalar, Vector2 vector) => vector * scalar;

        public static Vector2 operator *(Vector2 vector, float scalar) =>
            new(vector.X * scalar, vector.Y * scalar);

        public static Vector2 operator /(Vector2 vector, float scalar) =>
            new(vector.X / scalar, vector.Y / scalar);

        public static Vector2 operator +(Vector2 first, Vector2 second) =>
            new(first.X + second.X, first.Y + second.Y);

        public static Vector2 operator -(Vector2 first, Vector2 second) =>
            new(first.X - second.X, first.Y - second.Y);

        public static Vector2 operator -(Vector2 vector) => new(-vector.X, -vector.Y);
    }
}
