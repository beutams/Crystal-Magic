using UnityEngine;

namespace CrystalMagic.Game.MapDemo
{
    [DisallowMultipleComponent]
    public sealed class TunnelingMapDemo : MonoBehaviour
    {
        private const string GeneratedRootName = "__GeneratedTunnelingDemo";

        [Header("生成")]
        [SerializeField] private int _随机种子 = DungeonMakerTunnelingGenerator.DefaultSeed;
        [SerializeField] private bool _生成时随机种子 = true;
        [SerializeField, Min(0.25f)] private float _单格尺寸 = 1f;
        [SerializeField] private bool _运行时启动自动生成;
        [SerializeField] private DungeonMakerTunnelingConfig _配置 = DungeonMakerTunnelingConfig.CreateDefault();

        [Header("预览")]
        [SerializeField] private Color _走廊颜色 = new(0.18f, 0.20f, 0.24f);
        [SerializeField] private Color _房间颜色 = new(0.28f, 0.24f, 0.16f);
        [SerializeField] private Color _前厅颜色 = new(0.17f, 0.26f, 0.30f);
        [SerializeField] private Color _墙体颜色 = new(0.08f, 0.09f, 0.11f);

        [Header("最近结果")]
        [SerializeField] private int _最近生成种子;

        private DungeonMakerTunnelingResult _最近结果;
        private Transform _生成根节点;
        private Texture2D _预览纹理;
        private Sprite _预览精灵;

        public int Seed => _随机种子;
        public int LastGeneratedSeed => _最近生成种子;
        public DungeonMakerTunnelingConfig Config => _配置;

        [ContextMenu("生成 DEMO 地图")]
        public void GenerateDemoMap()
        {
            if (_生成时随机种子)
                _随机种子 = Random.Range(int.MinValue, int.MaxValue);

            ValidateParameters();
            _最近结果 = DungeonMakerTunnelingGenerator.Generate(_随机种子, _配置);
            _最近生成种子 = _最近结果.Seed;
            RebuildVisuals();
        }

        [ContextMenu("使用新种子生成 DEMO 地图")]
        public void GenerateDemoMapWithNewSeed()
        {
            int originalSeed = _随机种子;
            bool originalRandomize = _生成时随机种子;
            _生成时随机种子 = true;
            GenerateDemoMap();
            _生成时随机种子 = originalRandomize;
            _随机种子 = originalRandomize ? originalSeed : _最近生成种子;
        }

        [ContextMenu("清空 DEMO 地图")]
        public void ClearDemoMap()
        {
            _最近结果 = null;
            _最近生成种子 = 0;
            DestroyGeneratedRoot();
            DestroyPreviewAssets();
        }

        private void Start()
        {
            if (Application.isPlaying && _运行时启动自动生成)
                GenerateDemoMap();
        }

        private void OnDestroy()
        {
            DestroyPreviewAssets();
        }

        private void OnValidate()
        {
            ValidateParameters();
        }

        private void ValidateParameters()
        {
            _单格尺寸 = Mathf.Max(0.25f, _单格尺寸);
            _配置 ??= DungeonMakerTunnelingConfig.CreateDefault();
            _配置.DimX = Mathf.Max(3, _配置.DimX);
            _配置.DimY = Mathf.Max(3, _配置.DimY);
            _配置.MaxRoomSize = Mathf.Max(1, _配置.MaxRoomSize);
            _配置.MinSmallRoomSize = Mathf.Clamp(_配置.MinSmallRoomSize, 1, _配置.MaxRoomSize);
            _配置.MinMediumRoomSize = Mathf.Clamp(_配置.MinMediumRoomSize, _配置.MinSmallRoomSize, _配置.MaxRoomSize);
            _配置.MinLargeRoomSize = Mathf.Clamp(_配置.MinLargeRoomSize, _配置.MinMediumRoomSize, _配置.MaxRoomSize);
            _配置.RoomAspectRatio = Mathf.Max(0.01f, (float)_配置.RoomAspectRatio);
            EnsureCollections();
        }

        private void EnsureCollections()
        {
            _配置.BabyDelayProbsTunneler ??= new();
            _配置.BabyDelayProbsRoomie ??= new();
            _配置.MaxAgesT ??= new();
            _配置.RoomSizeProbS ??= new();
            _配置.RoomSizeProbB ??= new();
            _配置.JoinPref ??= new();
            _配置.SizeUpProb ??= new();
            _配置.SizeDownProb ??= new();
            _配置.AnteRoomProb ??= new();
            _配置.Tunnelers ??= System.Array.Empty<DungeonMakerTunnelerSeedData>();
        }

        private void RebuildVisuals()
        {
            DestroyGeneratedRoot();
            DestroyPreviewAssets();

            if (_最近结果 == null)
                return;

            _生成根节点 = new GameObject(GeneratedRootName).transform;
            _生成根节点.SetParent(transform, false);
            CreateSpritePreview();
        }

        private void CreateSpritePreview()
        {
            GameObject child = new("Map");
            child.transform.SetParent(_生成根节点, false);
            child.transform.localPosition = Vector3.zero;

            _预览纹理 = BuildPreviewTexture();
            _预览精灵 = Sprite.Create(
                _预览纹理,
                new Rect(0f, 0f, _预览纹理.width, _预览纹理.height),
                new Vector2(0.5f, 0.5f),
                1f / _单格尺寸,
                0,
                SpriteMeshType.FullRect);

            SpriteRenderer spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = _预览精灵;
            spriteRenderer.sortingOrder = 0;
        }

        private Texture2D BuildPreviewTexture()
        {
            Texture2D texture = new(_最近结果.DisplayWidth, _最近结果.DisplayHeight, TextureFormat.RGBA32, false)
            {
                name = "TunnelingDemoPreview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color[] pixels = new Color[_最近结果.DisplayWidth * _最近结果.DisplayHeight];
            for (int y = 0; y < _最近结果.DisplayHeight; y++)
            {
                for (int x = 0; x < _最近结果.DisplayWidth; x++)
                {
                    int pixelIndex = y * _最近结果.DisplayWidth + x;
                    pixels[pixelIndex] = GetTileColor(_最近结果.GetDisplayTile(x, y));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private Color GetTileColor(DungeonMakerSquareData tile)
        {
            return tile switch
            {
                DungeonMakerSquareData.IR_OPEN => _房间颜色,
                DungeonMakerSquareData.H_DOOR => _房间颜色,
                DungeonMakerSquareData.V_DOOR => _房间颜色,
                DungeonMakerSquareData.IA_OPEN => _前厅颜色,
                DungeonMakerSquareData.OPEN => _走廊颜色,
                DungeonMakerSquareData.G_OPEN => _走廊颜色,
                DungeonMakerSquareData.NJ_OPEN => _走廊颜色,
                DungeonMakerSquareData.NJ_G_OPEN => _走廊颜色,
                DungeonMakerSquareData.IT_OPEN => _走廊颜色,
                _ => _墙体颜色,
            };
        }

        private void DestroyGeneratedRoot()
        {
            Transform existingRoot = transform.Find(GeneratedRootName);
            if (existingRoot == null)
                return;

            if (Application.isPlaying)
                Destroy(existingRoot.gameObject);
            else
                DestroyImmediate(existingRoot.gameObject);

            _生成根节点 = null;
        }

        private void DestroyPreviewAssets()
        {
            DestroyPreviewObject(_预览精灵);
            DestroyPreviewObject(_预览纹理);
            _预览精灵 = null;
            _预览纹理 = null;
        }

        private static void DestroyPreviewObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
