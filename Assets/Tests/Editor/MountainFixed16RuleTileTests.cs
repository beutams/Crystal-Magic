using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.Game.OpenField;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MountainFixed16RuleTileTests
{
    private const string Sheet = "Assets/Res/Sprites/Dungeon/MountainFixed16.png";
    private const string TileRoot = "Assets/Res/Tile/Mountain/Mountain";
    private const string ArtRoot = "Tools/Art/MountainFixed16/";
    private static readonly Vector3Int[] Directions =
    {
        new(0,1),new(1,1),new(1,0),new(1,-1),new(0,-1),new(-1,-1),new(-1,0),new(-1,1),
    };
    private static MountainRuleTile Load(string part) => AssetDatabase.LoadAssetAtPath<MountainRuleTile>(TileRoot + part + "_Fixed16.asset");
    private static Texture2D Read(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True);
        return texture;
    }

    [Test]
    public void AllPartsImportAndResolveEveryTernaryCase()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Sheet);
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        Assert.That(importer.mipmapEnabled, Is.False);
        foreach (string part in new[] { "Foot", "Wall", "Top" })
        {
            var tile = Load(part);
            Assert.That(tile, Is.Not.Null);
            Assert.That(tile.neighborPositions.Count, Is.EqualTo(8));
            Assert.That(tile.VariantLookup.Length, Is.EqualTo(6561));
            Assert.That(tile.m_DefaultColliderType, Is.EqualTo(Tile.ColliderType.None));
            foreach (var sprite in tile.VariantSprites)
            {
                Assert.That(sprite, Is.Not.Null);
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(16,16)));
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16));
                Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(Sheet));
            }
            for (int key = 0; key < 6561; key++)
            {
                Assert.That(tile.VariantLookup[key], Is.InRange(0,tile.VariantSprites.Length-1));
                Assert.That(tile.GetSpriteForNeighbourKey(key), Is.SameAs(tile.VariantSprites[tile.VariantLookup[key]]));
            }
        }
    }

    [Test]
    public void All27MastersAreUnchangedAndEveryCompositePixelComesFromThem()
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().ToDictionary(s => s.name);
        var sheet = Read(Sheet);
        var masters = Directory.GetFiles(ArtRoot + "Masters", "*.png").ToDictionary(Path.GetFileNameWithoutExtension, Read);
        try
        {
            Assert.That(masters.Count, Is.EqualTo(27));
            foreach (var pair in masters)
            {
                Sprite sprite = sprites[pair.Key];
                for (int y=0;y<16;y++) for(int x=0;x<16;x++)
                    Assert.That((Color32)sheet.GetPixel((int)sprite.rect.x+x,(int)sprite.rect.y+y),
                        Is.EqualTo((Color32)pair.Value.GetPixel(x,y)), pair.Key+" "+x+","+y);
            }
            JObject manifest = JObject.Parse(File.ReadAllText(ArtRoot + "atlas-manifest.json"));
            foreach (JObject entry in manifest["Sprites"])
            {
                var sprite = sprites[(string)entry["Name"]];
                string[] sources = entry["Sources"].Values<string>().ToArray();
                for (int y=0;y<16;y++) for(int x=0;x<16;x++)
                {
                    Color32 pixel = sheet.GetPixel((int)sprite.rect.x+x,(int)sprite.rect.y+y);
                    Assert.That(sources.Any(s => ((Color32)masters[s].GetPixel(x,y)).Equals(pixel)), Is.True,
                        sprite.name+" contains a repainted pixel at "+x+","+y);
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
            foreach(var texture in masters.Values) UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void FrontCapHasRockUnderlayButRearCapRevealsTheBackground()
    {
        var top=Load("Top");var sheet=Read(Sheet);
        try
        {
            Sprite rear=top.GetSpriteForNeighbourKey(6560-2);
            Sprite front=top.GetSpriteForNeighbourKey(6560-81); // S is cliff, not summit.
            Assert.That(((Color32)sheet.GetPixel((int)rear.rect.x+8,(int)rear.rect.y+15)).a,Is.Zero);
            Assert.That(((Color32)sheet.GetPixel((int)front.rect.x+8,(int)front.rect.y)).a,Is.EqualTo(255));
            Assert.That(top.GetSpriteForNeighbourKey(6560),Is.SameAs(top.m_DefaultSprite));
        }
        finally { UnityEngine.Object.DestroyImmediate(sheet); }
    }

    [TestCase(2,9,15)]
    [TestCase(6,729,0)]
    public void SummitCliffContactIsSolidButOutsideRetainsItsTransparentBorder(int direction,int weight,int x)
    {
        var top=Load("Top");var sheet=Read(Sheet);
        try
        {
            Sprite connected=top.GetSpriteForNeighbourKey(6560-weight);
            Sprite outside=top.GetSpriteForNeighbourKey(6560-2*weight);
            Assert.That(connected,Is.Not.SameAs(outside),"Cliff and outside collapsed at direction "+direction);
            for(int y=0;y<16;y++)
            {
                Assert.That(((Color32)sheet.GetPixel((int)connected.rect.x+x,(int)connected.rect.y+y)).a,Is.EqualTo(255),"Internal hole at "+y);
                Assert.That(((Color32)sheet.GetPixel((int)outside.rect.x+x,(int)outside.rect.y+y)).a,Is.Zero);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(sheet); }
    }

    [TestCase("Wall")]
    [TestCase("Foot")]
    public void MissingDiagonalNeverCreatesAFullHeightOuterEdgeBetweenTwoCliffs(string part)
    {
        var tile=Load(part);
        foreach(int weight in new[]{3,27,243,2187})
            Assert.That(tile.GetSpriteForNeighbourKey(3280-weight),Is.SameAs(tile.GetSpriteForNeighbourKey(3280)),
                "A missing diagonal created a false full-height outside edge: "+weight);
    }

    [TestCase(3,15)]
    [TestCase(2187,0)]
    public void RearInsetCornerIsFilledOnlyWhenTheDiagonalContainsACliff(int weight,int x)
    {
        var top=Load("Top");var sheet=Read(Sheet);
        try
        {
            Sprite cliff=top.GetSpriteForNeighbourKey(6560-weight);
            Sprite outside=top.GetSpriteForNeighbourKey(6560-2*weight);
            Assert.That(((Color32)sheet.GetPixel((int)cliff.rect.x+x,(int)cliff.rect.y+15)).a,Is.EqualTo(255));
            Assert.That(((Color32)sheet.GetPixel((int)outside.rect.x+x,(int)outside.rect.y+15)).a,Is.Zero);
        }
        finally { UnityEngine.Object.DestroyImmediate(sheet); }
    }

    [Test]
    public void ExposedCliffCrestsDoNotStickAboveTheRearRim()
    {
        var wall=Load("Wall");var sheet=Read(Sheet);var rim=Read(ArtRoot+"Masters/top-N.png");
        try
        {
            var sprite=wall.GetSpriteForNeighbourKey(3280-1);
            for(int y=0;y<16;y++)for(int x=0;x<16;x++)
                if(((Color32)rim.GetPixel(x,y)).a==0)
                    Assert.That(((Color32)sheet.GetPixel((int)sprite.rect.x+x,(int)sprite.rect.y+y)).a,Is.Zero);
        }
        finally { UnityEngine.Object.DestroyImmediate(sheet);UnityEngine.Object.DestroyImmediate(rim); }
    }

    [Test]
    public void EveryInternalLateralCliffContactHasNoTransparentColumn()
    {
        var sheet=Read(Sheet);
        try
        {
            foreach(string part in new[]{"Top","Wall","Foot"})
            {
                var tile=Load(part);
                for(int key=0;key<6561;key++)
                {
                    if(key%3==0)continue; // Open rear headroom is intentionally transparent.
                    var sprite=tile.GetSpriteForNeighbourKey(key);
                    foreach(int direction in new[]{2,6})
                    {
                        int weight=direction==2?9:729;
                        if(key/weight%3!=1)continue;
                        int x=direction==2?15:0;
                        for(int y=0;y<16;y++)
                            Assert.That(((Color32)sheet.GetPixel((int)sprite.rect.x+x,(int)sprite.rect.y+y)).a,Is.EqualTo(255),
                                $"{part} key={key} direction={direction} y={y}");
                    }
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(sheet); }
    }

    [Test]
    public void NewFamilyConnectsAndNativeTilemapRefreshesAfterNeighbourChanges()
    {
        var foot=Load("Foot");var wall=Load("Wall");var top=Load("Top");
        foreach(var a in new[]{foot,wall,top}) foreach(var b in new[]{foot,wall,top})
            Assert.That(a.GetNeighbourState(b),Is.EqualTo(b==top?2:1));
        Assert.That(top.GetNeighbourState(AssetDatabase.LoadAssetAtPath<RuleTile>("Assets/Res/Tile/Grass1.asset")),Is.Zero);
        WithMap(map =>
        {
            foreach(var centre in new[]{foot,wall,top})
            {
                map.ClearAllTiles();map.SetTile(Vector3Int.zero,centre);
                foreach(int key in new[]{0,3280,6560,6558,5831,5102,2916,4374,728,729,81,3279})
                {
                    int value=key;
                    foreach(var direction in Directions)
                    {
                        int state=value%3;value/=3;
                        map.SetTile(direction,state==0?null:state==1?wall:top);
                    }
                    Assert.That(map.GetSprite(Vector3Int.zero),Is.SameAs(centre.GetSpriteForNeighbourKey(key)));
                    Assert.That(map.GetTransformMatrix(Vector3Int.zero),Is.EqualTo(Matrix4x4.identity));
                }
            }
        });
    }

    [Test]
    public void ConfiguredThemeUsesNewTilesAndTwoWallRows()
    {
        JObject table=JObject.Parse(File.ReadAllText("Assets/Res/Data/DungeonThemeDataTable.json"));
        var field=table["Rows"].Single(t => (string)t["Name"]=="Prairie")["OpenField"];
        Assert.That((int)field["Terrain"]["MinimumObstacleHeight"],Is.EqualTo(2));
        Assert.That((int)field["Terrain"]["MaximumObstacleHeight"],Is.EqualTo(2));
        foreach(var path in field["Visual"].SelectTokens("$..AssetPath").Values<string>().Where(p => p.Contains("/Mountain/")))
        {
            Assert.That(path,Does.EndWith("_Fixed16.asset"));
            Assert.That(AssetDatabase.LoadAssetAtPath<MountainRuleTile>(path),Is.Not.Null);
        }
        var layout=MakeLayout(12,12);
        FillGround(layout);
        for(int y=2;y<=7;y++) for(int x=2;x<=9;x++) Set(layout,x,y,true);
        var placements=Place(layout).Where(p=>p.Layer==OpenFieldRuleTileLayer.Obstacle)
            .GroupBy(p=>p.Cell).ToDictionary(g=>g.Key,g=>g.Last());
        Assert.That(placements[new Vector2Int(5,2)].RuleTile.AssetPath,Does.EndWith("Foot_Fixed16.asset"));
        Assert.That(placements[new Vector2Int(5,3)].RuleTile.AssetPath,Does.EndWith("Wall_Fixed16.asset"));
        Assert.That(placements[new Vector2Int(5,4)].RuleTile.AssetPath,Does.EndWith("Wall_Fixed16.asset"));
        Assert.That(placements[new Vector2Int(5,5)].RuleTile.AssetPath,Does.EndWith("Top_Fixed16.asset"));
    }

    [Test]
    public void RuntimePlacementsResolveConcaveConvexAndNarrowMountainsAndExportPreview()
    {
        var layout=MakeLayout(24,20);FillGround(layout);
        for(int y=2;y<=13;y++) for(int x=2;x<=14;x++)
            if(!((x>=6&&x<=10)&&(y<6||y>10))) Set(layout,x,y,true);
        for(int y=2;y<=10;y++) Set(layout,18,y,true);
        Set(layout,21,4,true);Set(layout,21,7,true);Set(layout,22,7,true);
        ExportLayoutPreview(layout,"runtime-preview");
    }

    [Test]
    public void RuntimePlacementsResolveRepeatedSingleCellStepsWithoutLosingCliffContacts()
    {
        var layout=MakeLayout(24,20);FillGround(layout);
        int[] fronts={3,3,4,4,5,4,5,7,6,7,8,9,10,9,8,7,6,4,4,3};
        for(int i=0;i<fronts.Length;i++)for(int y=fronts[i];y<=14+(i%7==0?1:0);y++)Set(layout,i+2,y,true);
        ExportLayoutPreview(layout,"stepped-preview");
    }

    private static void ExportLayoutPreview(OpenFieldDungeonLayout layout,string filename)
    {
        var final=Place(layout).Where(p=>p.Layer==OpenFieldRuleTileLayer.Obstacle)
            .GroupBy(p=>p.Cell).ToDictionary(g=>g.Key,g=>g.Last());
        var sheet=Read(Sheet);var image=new Texture2D(24*16,20*16,TextureFormat.RGBA32,false);
        image.SetPixels32(Enumerable.Repeat(new Color32(140,169,66,255),image.width*image.height).ToArray());
        var used=new HashSet<string>();
        try
        {
            WithMap(map =>
            {
                foreach(var p in final.Values) map.SetTile((Vector3Int)p.Cell,AssetDatabase.LoadAssetAtPath<MountainRuleTile>(p.RuleTile.AssetPath));
                map.RefreshAllTiles();
                foreach(var p in final.Values)
                {
                    var sprite=map.GetSprite((Vector3Int)p.Cell);
                    Assert.That(sprite,Is.Not.Null,p.Cell.ToString());used.Add(sprite.name);
                    for(int y=0;y<16;y++) for(int x=0;x<16;x++)
                    {
                        Color32 color=sheet.GetPixel((int)sprite.rect.x+x,(int)sprite.rect.y+y);
                        if(color.a>0) image.SetPixel(p.Cell.x*16+x,p.Cell.y*16+y,color);
                    }
                }
            });
            Assert.That(used.Any(s=>s.StartsWith("summit_")),Is.True);
            Assert.That(used.Contains("wall-front"),Is.True);
            Assert.That(used.Contains("foot-front"),Is.True);
            Directory.CreateDirectory("Temp/MountainFixed16Validation");
            File.WriteAllBytes("Temp/MountainFixed16Validation/"+filename+".png",image.EncodeToPNG());
            File.WriteAllText("Temp/MountainFixed16Validation/"+filename+"-sprites.txt",string.Join("\n",used.OrderBy(s=>s)));
        }
        finally { UnityEngine.Object.DestroyImmediate(sheet);UnityEngine.Object.DestroyImmediate(image); }
    }

    private static OpenFieldDungeonLayout MakeLayout(int width,int height) =>
        (OpenFieldDungeonLayout)Activator.CreateInstance(typeof(OpenFieldDungeonLayout),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{width,height,123},null);
    private static void Set(OpenFieldDungeonLayout layout,int x,int y,bool mountain) =>
        typeof(OpenFieldDungeonLayout).GetMethod("SetTerrain",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(layout,
            new object[]{x,y,0.5f,mountain?OpenFieldTerrainCell.Obstacle:OpenFieldTerrainCell.Ground,mountain?2:0});
    private static void FillGround(OpenFieldDungeonLayout layout)
    {
        for(int y=0;y<layout.Height;y++)for(int x=0;x<layout.Width;x++)Set(layout,x,y,false);
    }
    private static List<OpenFieldRuleTilePlacement> Place(OpenFieldDungeonLayout layout)
    {
        var visual=new OpenFieldDungeonVisualData { GroundStyles=new List<OpenFieldGroundStyleData>{new()} };
        visual.ObstacleVisual.TransitionRuleTile.AssetPath=TileRoot+"Foot_Fixed16.asset";
        visual.ObstacleVisual.WallRuleTile.AssetPath=TileRoot+"Wall_Fixed16.asset";
        visual.ObstacleVisual.TopRuleTile.AssetPath=TileRoot+"Top_Fixed16.asset";
        return (List<OpenFieldRuleTilePlacement>)typeof(OpenFieldDungeonVisualLayoutBuilder)
            .GetMethod("CreateTerrainPlacements",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{layout,visual,new int[layout.CellCount]});
    }
    private static void WithMap(Action<Tilemap> action)
    {
        var scene=EditorSceneManager.NewPreviewScene();GameObject root=null;
        try
        {
            root=new GameObject("MountainFixed16Validation",typeof(Grid)){hideFlags=HideFlags.HideAndDontSave};
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var child=new GameObject("Tiles",typeof(Tilemap)){hideFlags=HideFlags.HideAndDontSave};
            child.transform.SetParent(root.transform,false);action(child.GetComponent<Tilemap>());
        }
        finally { if(root!=null)UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene); }
    }
}
