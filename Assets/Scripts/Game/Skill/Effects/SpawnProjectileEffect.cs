using System.Collections.Generic;
using CrystalMagic.Game.Data.Effects;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace CrystalMagic.Game.Skill.Effects
{
    /// <summary>
    /// 创建投射物效果，逻辑由投射物系统接入
    /// </summary>
    public sealed class SpawnProjectileEffect : Effect
    {
        public new SpawnProjectileEffectData Data { get; }

        public SpawnProjectileEffect(SpawnProjectileEffectData data) : base(data) => Data = data;

        public override void Execute(SkillContent context)
        {
            if (Data == null || Data.Projectile == null || context == null)
                return;

            if (!TryGetSpawnPosition(context, out Vector3 spawnPosition))
                return;

            Vector3 direction = GetProjectileDirection(context, spawnPosition);
            Quaternion rotation = Quaternion.AngleAxis(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, Vector3.forward);
            Vector3 finalPosition = spawnPosition + rotation * Data.SpawnOffset;

            GameObject projectile = Object.Instantiate(Data.Projectile, finalPosition, rotation);
            projectile.transform.localScale *= Data.Scale;

            SkillProjectileRuntime runtime = projectile.GetComponent<SkillProjectileRuntime>();
            if (runtime == null)
                runtime = projectile.AddComponent<SkillProjectileRuntime>();

            runtime.Initialize(Data, context, finalPosition, direction);
        }

        private bool TryGetSpawnPosition(SkillContent context, out Vector3 position)
        {
            if (context.HasOriginEntity &&
                context.OriginEntity != Entity.Null &&
                context.EntityManager.Exists(context.OriginEntity) &&
                context.EntityManager.HasComponent<LocalTransform>(context.OriginEntity))
            {
                float3 entityPosition = context.EntityManager.GetComponentData<LocalTransform>(context.OriginEntity).Position;
                position = new Vector3(entityPosition.x, entityPosition.y, entityPosition.z);
                return true;
            }

            if (context.HasPosition)
            {
                position = context.Position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private static Vector3 GetProjectileDirection(SkillContent context, Vector3 spawnPosition)
        {
            if (context.HasPosition)
            {
                Vector3 direction = context.Position - spawnPosition;
                direction.z = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                    return direction.normalized;
            }

            return Vector3.right;
        }
    }

    public sealed class SkillProjectileRuntime : MonoBehaviour
    {
        private const float HitSearchRadius = 0.75f;

        private readonly List<UnitQueryHit> _hits = new();
        private readonly HashSet<Entity> _hitEntities = new();
        private readonly HashSet<int> _hitObjects = new();
        private SkillContent _context;
        private EffectData[] _onCollisionEffects;
        private EffectData[] _onDestroyEffects;
        private Vector3 _direction;
        private float _speed;
        private float _maxRange;
        private float _traveledDistance;
        private bool _canPierce;
        private bool _triggerDestroyEffectsOnMaxRange;
        private bool _destroyed;

        public void Initialize(SpawnProjectileEffectData data, SkillContent context, Vector3 startPosition, Vector3 direction)
        {
            transform.position = startPosition;
            _context = context.Clone();
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
            _speed = data.Speed;
            _maxRange = data.MaxRange;
            _canPierce = data.CanPierce;
            _triggerDestroyEffectsOnMaxRange = data.TriggerDestroyEffectsOnMaxRange;
            _onCollisionEffects = data.OnCollisionEffects;
            _onDestroyEffects = data.OnDestroyEffects;
            _traveledDistance = 0f;
            _destroyed = false;
            _hitEntities.Clear();
            _hitObjects.Clear();
        }

        private void Update()
        {
            if (_destroyed)
                return;

            float moveDistance = _speed * Time.deltaTime;
            transform.position += _direction * moveDistance;
            _traveledDistance += Mathf.Abs(moveDistance);

            if (_maxRange > 0f && _traveledDistance >= _maxRange)
                DestroyProjectile(_triggerDestroyEffectsOnMaxRange, null, transform.position);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleCollision(other.gameObject, other.ClosestPoint(transform.position));
        }

        private void OnCollisionEnter(Collision collision)
        {
            Vector3 hitPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.transform.position;
            HandleCollision(collision.gameObject, hitPoint);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Vector2 point = other.ClosestPoint(transform.position);
            HandleCollision(other.gameObject, new Vector3(point.x, point.y, transform.position.z));
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            Vector2 point = collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.transform.position;
            HandleCollision(collision.gameObject, new Vector3(point.x, point.y, transform.position.z));
        }

        private void HandleCollision(GameObject hitObject, Vector3 hitPoint)
        {
            if (_destroyed || hitObject == null)
                return;

            SkillContent hitContext = BuildHitContext(hitObject, hitPoint, out Entity hitEntity);
            if (!RegisterHit(hitObject, hitEntity))
                return;

            SkillExecutor.ExecuteEffects(_onCollisionEffects, hitContext);

            if (!_canPierce)
                DestroyProjectile(triggerDestroyEffects: true, hitContext, hitPoint);
        }

        private SkillContent BuildHitContext(GameObject hitObject, Vector3 hitPoint, out Entity hitEntity)
        {
            SkillContent hitContext = _context.Clone();
            hitContext.EntityManager = GetEntityManager();
            hitContext.HasPosition = true;
            hitContext.Position = hitPoint;
            hitContext.HasTarget = true;
            hitContext.Target = hitObject;

            if (TryResolveHitEntity(hitPoint, out hitEntity))
            {
                hitContext.HasTargetEntity = true;
                hitContext.TargetEntity = hitEntity;
            }
            else
            {
                hitContext.HasTargetEntity = false;
                hitContext.TargetEntity = Entity.Null;
            }

            return hitContext;
        }

        private bool RegisterHit(GameObject hitObject, Entity hitEntity)
        {
            if (hitEntity != Entity.Null)
                return _hitEntities.Add(hitEntity);

            return _hitObjects.Add(hitObject.GetInstanceID());
        }

        private bool TryResolveHitEntity(Vector3 hitPoint, out Entity hitEntity)
        {
            hitEntity = Entity.Null;

            UnitQuerySystem querySystem = UnitQuerySystem.Default;
            if (querySystem == null)
                return false;

            querySystem.QueryCircle(new float3(hitPoint.x, hitPoint.y, hitPoint.z), HitSearchRadius, _hits);
            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < _hits.Count; i++)
            {
                UnitQueryHit hit = _hits[i];
                if (_context.HasOriginEntity && hit.Entity == _context.OriginEntity)
                    continue;

                float2 diff = hit.Position.xy - new float2(hitPoint.x, hitPoint.y);
                float distanceSq = math.lengthsq(diff);
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                hitEntity = hit.Entity;
            }

            return hitEntity != Entity.Null;
        }

        private void DestroyProjectile(bool triggerDestroyEffects, SkillContent destroyContext, Vector3 destroyPosition)
        {
            if (_destroyed)
                return;

            _destroyed = true;
            if (triggerDestroyEffects)
            {
                SkillContent context = destroyContext?.Clone() ?? _context.Clone();
                context.EntityManager = GetEntityManager();
                context.HasPosition = true;
                context.Position = destroyPosition;
                SkillExecutor.ExecuteEffects(_onDestroyEffects, context);
            }

            Destroy(gameObject);
        }

        private static EntityManager GetEntityManager()
        {
            UnitQuerySystem querySystem = UnitQuerySystem.Default;
            return querySystem != null
                ? querySystem.QueryEntityManager
                : World.DefaultGameObjectInjectionWorld.EntityManager;
        }
    }
}
