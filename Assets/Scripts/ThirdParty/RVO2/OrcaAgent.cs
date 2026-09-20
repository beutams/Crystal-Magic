/*
 * Derived from RVO2 Library C# Agent.cs and Vector2.cs.
 * SPDX-FileCopyrightText: 2008 University of North Carolina at Chapel Hill
 * SPDX-License-Identifier: Apache-2.0
 *
 * Modified for Crystal Magic: this is an unmanaged, Burst-compatible ORCA solver.
 * Neighbor discovery is supplied by the game's ECS UnitQuery buffer and ECS remains
 * the authoritative owner of positions and velocities.
 */

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CrystalMagic.ThirdParty.RVO2
{
    internal struct AgentData
    {
        internal Entity Entity;
        internal int MaxNeighbors;
        internal float MaxSpeed;
        internal float NeighborDistance;
        internal float Radius;
        internal float TimeHorizon;
        internal float2 Position;
        internal float2 PreferredVelocity;
        internal float2 Velocity;
        internal byte HasFrameVelocity;
    }

    internal struct AgentNeighbor
    {
        internal float DistanceSq;
        internal Entity Entity;
        internal float2 Position;
        internal float2 Velocity;
        internal float Radius;
    }

    internal struct OrcaLine
    {
        internal float2 Direction;
        internal float2 Point;
    }

    internal static class OrcaSolver
    {
        private const float Epsilon = 0.00001f;

        internal static void InsertNeighbor(
            in AgentData self,
            in AgentData other,
            ref FixedList4096Bytes<AgentNeighbor> neighbors,
            ref float rangeSq)
        {
            int maxNeighbors = math.min(math.max(0, self.MaxNeighbors), neighbors.Capacity);
            if (maxNeighbors <= 0 || self.Entity == other.Entity)
                return;

            float distanceSq = math.lengthsq(self.Position - other.Position);
            if (distanceSq >= rangeSq)
                return;

            AgentNeighbor candidate = new()
            {
                DistanceSq = distanceSq,
                Entity = other.Entity,
                Position = other.Position,
                Velocity = other.Velocity,
                Radius = other.Radius,
            };
            if (neighbors.Length < maxNeighbors)
                neighbors.Add(candidate);

            int index = math.min(neighbors.Length - 1, maxNeighbors - 1);
            while (index > 0)
            {
                AgentNeighbor previous = neighbors[index - 1];
                if (!IsBefore(in candidate, in previous))
                    break;
                neighbors[index] = neighbors[index - 1];
                index--;
            }

            neighbors[index] = candidate;
            if (neighbors.Length == maxNeighbors)
                rangeSq = neighbors[neighbors.Length - 1].DistanceSq;
        }

        internal static float2 ComputeNewVelocity(
            in AgentData self,
            in FixedList4096Bytes<AgentNeighbor> neighbors,
            float timeStep)
        {
            FixedList4096Bytes<OrcaLine> lines = default;
            float inverseTimeHorizon = 1f / math.max(Epsilon, self.TimeHorizon);
            float inverseTimeStep = 1f / math.max(Epsilon, timeStep);

            for (int index = 0; index < neighbors.Length && lines.Length < lines.Capacity; index++)
            {
                AgentNeighbor other = neighbors[index];
                float2 relativePosition = other.Position - self.Position;
                float2 relativeVelocity = self.Velocity - other.Velocity;
                float distanceSq = math.lengthsq(relativePosition);
                float combinedRadius = self.Radius + other.Radius;
                float combinedRadiusSq = combinedRadius * combinedRadius;

                OrcaLine line;
                float2 correction;
                if (distanceSq > combinedRadiusSq)
                {
                    float2 w = relativeVelocity - inverseTimeHorizon * relativePosition;
                    float wLengthSq = math.lengthsq(w);
                    float dotProduct = math.dot(w, relativePosition);

                    if (dotProduct < 0f && dotProduct * dotProduct > combinedRadiusSq * wLengthSq)
                    {
                        float wLength = math.sqrt(wLengthSq);
                        float2 unitW = w / wLength;
                        line.Direction = new float2(unitW.y, -unitW.x);
                        correction = (combinedRadius * inverseTimeHorizon - wLength) * unitW;
                    }
                    else
                    {
                        float leg = math.sqrt(math.max(0f, distanceSq - combinedRadiusSq));
                        if (Det(relativePosition, w) > 0f)
                        {
                            line.Direction = new float2(
                                relativePosition.x * leg - relativePosition.y * combinedRadius,
                                relativePosition.x * combinedRadius + relativePosition.y * leg) / distanceSq;
                        }
                        else
                        {
                            line.Direction = -new float2(
                                relativePosition.x * leg + relativePosition.y * combinedRadius,
                                -relativePosition.x * combinedRadius + relativePosition.y * leg) / distanceSq;
                        }

                        correction = math.dot(relativeVelocity, line.Direction) * line.Direction - relativeVelocity;
                    }
                }
                else
                {
                    float2 w = relativeVelocity - inverseTimeStep * relativePosition;
                    float wLength = math.length(w);
                    float2 unitW;
                    if (wLength > Epsilon)
                        unitW = w / wLength;
                    else if (distanceSq > Epsilon * Epsilon)
                        unitW = math.normalizesafe(-relativePosition);
                    else
                        unitW = IsEntityBefore(self.Entity, other.Entity)
                            ? new float2(1f, 0f)
                            : new float2(-1f, 0f);

                    line.Direction = new float2(unitW.y, -unitW.x);
                    correction = (combinedRadius * inverseTimeStep - wLength) * unitW;
                }

                line.Point = self.Velocity + 0.5f * correction;
                lines.Add(line);
            }

            int failedLine = LinearProgram2(
                in lines,
                self.MaxSpeed,
                self.PreferredVelocity,
                false,
                out float2 result);
            if (failedLine < lines.Length)
                LinearProgram3(in lines, failedLine, self.MaxSpeed, ref result);
            return result;
        }

        private static bool IsBefore(in AgentNeighbor left, in AgentNeighbor right)
        {
            if (left.DistanceSq != right.DistanceSq)
                return left.DistanceSq < right.DistanceSq;
            return IsEntityBefore(left.Entity, right.Entity);
        }

        private static bool IsEntityBefore(Entity left, Entity right)
        {
            return left.Index != right.Index
                ? left.Index < right.Index
                : left.Version < right.Version;
        }

        private static bool LinearProgram1(
            in FixedList4096Bytes<OrcaLine> lines,
            int lineNumber,
            float radius,
            float2 optimalVelocity,
            bool directionOnly,
            ref float2 result)
        {
            OrcaLine selectedLine = lines[lineNumber];
            float dotProduct = math.dot(selectedLine.Point, selectedLine.Direction);
            float discriminant = dotProduct * dotProduct + radius * radius - math.lengthsq(selectedLine.Point);
            if (discriminant < 0f)
                return false;

            float sqrtDiscriminant = math.sqrt(discriminant);
            float left = -dotProduct - sqrtDiscriminant;
            float right = -dotProduct + sqrtDiscriminant;

            for (int index = 0; index < lineNumber; index++)
            {
                OrcaLine previousLine = lines[index];
                float denominator = Det(selectedLine.Direction, previousLine.Direction);
                float numerator = Det(
                    previousLine.Direction,
                    selectedLine.Point - previousLine.Point);

                if (math.abs(denominator) <= Epsilon)
                {
                    if (numerator < 0f)
                        return false;
                    continue;
                }

                float t = numerator / denominator;
                if (denominator >= 0f)
                    right = math.min(right, t);
                else
                    left = math.max(left, t);

                if (left > right)
                    return false;
            }

            if (directionOnly)
            {
                result = math.dot(optimalVelocity, selectedLine.Direction) > 0f
                    ? selectedLine.Point + right * selectedLine.Direction
                    : selectedLine.Point + left * selectedLine.Direction;
            }
            else
            {
                float t = math.dot(selectedLine.Direction, optimalVelocity - selectedLine.Point);
                result = t < left
                    ? selectedLine.Point + left * selectedLine.Direction
                    : t > right
                        ? selectedLine.Point + right * selectedLine.Direction
                        : selectedLine.Point + t * selectedLine.Direction;
            }

            return true;
        }

        private static int LinearProgram2(
            in FixedList4096Bytes<OrcaLine> lines,
            float radius,
            float2 optimalVelocity,
            bool directionOnly,
            out float2 result)
        {
            if (directionOnly)
                result = optimalVelocity * radius;
            else if (math.lengthsq(optimalVelocity) > radius * radius)
                result = math.normalizesafe(optimalVelocity) * radius;
            else
                result = optimalVelocity;

            for (int index = 0; index < lines.Length; index++)
            {
                OrcaLine line = lines[index];
                if (Det(line.Direction, line.Point - result) <= 0f)
                    continue;

                float2 previousResult = result;
                if (!LinearProgram1(in lines, index, radius, optimalVelocity, directionOnly, ref result))
                {
                    result = previousResult;
                    return index;
                }
            }

            return lines.Length;
        }

        private static void LinearProgram3(
            in FixedList4096Bytes<OrcaLine> lines,
            int firstFailedLine,
            float radius,
            ref float2 result)
        {
            float distance = 0f;
            for (int index = firstFailedLine; index < lines.Length; index++)
            {
                OrcaLine selectedLine = lines[index];
                if (Det(selectedLine.Direction, selectedLine.Point - result) <= distance)
                    continue;

                FixedList4096Bytes<OrcaLine> projectedLines = default;
                for (int previousIndex = 0;
                     previousIndex < index && projectedLines.Length < projectedLines.Capacity;
                     previousIndex++)
                {
                    OrcaLine previousLine = lines[previousIndex];
                    OrcaLine projectedLine;
                    float determinant = Det(selectedLine.Direction, previousLine.Direction);
                    if (math.abs(determinant) <= Epsilon)
                    {
                        if (math.dot(selectedLine.Direction, previousLine.Direction) > 0f)
                            continue;
                        projectedLine.Point = 0.5f * (selectedLine.Point + previousLine.Point);
                    }
                    else
                    {
                        projectedLine.Point = selectedLine.Point +
                                              Det(previousLine.Direction, selectedLine.Point - previousLine.Point) /
                                              determinant * selectedLine.Direction;
                    }

                    projectedLine.Direction = math.normalizesafe(previousLine.Direction - selectedLine.Direction);
                    projectedLines.Add(projectedLine);
                }

                float2 previousResult = result;
                float2 optimizationDirection = new(-selectedLine.Direction.y, selectedLine.Direction.x);
                if (LinearProgram2(
                        in projectedLines,
                        radius,
                        optimizationDirection,
                        true,
                        out result) < projectedLines.Length)
                {
                    result = previousResult;
                }

                distance = Det(selectedLine.Direction, selectedLine.Point - result);
            }
        }

        private static float Det(float2 left, float2 right)
        {
            return left.x * right.y - left.y * right.x;
        }
    }
}
