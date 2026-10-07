using System;
using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalMagic.Game.Map
{
    // This companion prefab belongs in a SubScene; the visual prefab stays outside.
    // Reuses the existing navigation component and its tightly paired bitset buffer.
    [DisallowMultipleComponent]
    public sealed class TileCollisionNavigationAuthoring : MonoBehaviour
    {
        public TileCollisionData Data;

        public static DungeonNavigationMapComponent Describe(TileCollisionData data, Transform root)
        {
            data.Validate();
            Vector3 right = root.TransformVector(Vector3.right), up = root.TransformVector(Vector3.up);
            if (right.x <= 0 || up.y <= 0 || Mathf.Abs(right.x - up.y) > 0.0001f ||
                Mathf.Abs(right.y) + Mathf.Abs(right.z) + Mathf.Abs(up.x) + Mathf.Abs(up.z) > 0.0001f)
                throw new InvalidOperationException("碰撞/寻路根节点只能平移、正向等比缩放，不能旋转或镜像。");
            Vector3 origin = root.TransformPoint(data.MinCorner);
            if (Mathf.Abs(origin.z) > 0.0001f)
                throw new InvalidOperationException("碰撞与角色须位于同一个 Z=0 平面。");
            int version = unchecked((int)math.hash(new float4(origin.x, origin.y, data.CellSize * right.x, 0)));
            unchecked
            {
                version = (version * 31 + data.Width) * 31 + data.Height;
                foreach (var cell in data.BlockedCells) version = (version * 31 + cell.x) * 31 + cell.y;
            }
            return new DungeonNavigationMapComponent
            {
                Width = data.Width, Height = data.Height, CellSize = data.CellSize * right.x,
                WorldOrigin = new float2(origin.x, origin.y), Version = version
            };
        }

        public static ulong[] BuildWords(TileCollisionData data)
        {
            data.Validate();
            var words = new ulong[DungeonNavigationMapUtility.GetRequiredWordCount(data.Width * data.Height)];
            foreach (Vector3Int cell in data.BlockedCells)
            {
                int index = (cell.y - data.MinCell.y) * data.Width + cell.x - data.MinCell.x;
                words[index >> 6] |= 1UL << (index & 63);
            }
            return words;
        }

        private sealed class CollisionNavigationBaker : Baker<TileCollisionNavigationAuthoring>
        {
            public override void Bake(TileCollisionNavigationAuthoring authoring)
            {
                if (authoring.Data == null) throw new InvalidOperationException("缺少碰撞占地配置。");
                DependsOn(authoring.Data);
                GetComponent<Transform>();
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, Describe(authoring.Data, authoring.transform));
                DynamicBuffer<DungeonNavigationCollisionWord> buffer = AddBuffer<DungeonNavigationCollisionWord>(entity);
                foreach (ulong word in BuildWords(authoring.Data))
                    buffer.Add(new DungeonNavigationCollisionWord { Value = word, ProjectileValue = word });
            }
        }
    }
}
