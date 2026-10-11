using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

// Offline, deterministic composition only. Every output pixel is copied from
// one of the 27 approved 16x16 masters, at the same coordinate. No repainting.
public static class BakeMountain
{
    public sealed class Entry { public string Name; public int Slot; public string[] Sources; }
    public sealed class Result
    {
        public List<Entry> Sprites;
        public int[][] Lookups;
        public int Width, Height;
    }
    static Dictionary<string,int[]> masters;
    static List<int[]> images;
    static List<Entry> entries;
    static Dictionary<string,int> cache;
    static Dictionary<string,int> compositePixels;
    static int[] Read(string file)
    {
        using(var b=new Bitmap(file)) {
            if(b.Width!=16||b.Height!=16)throw new Exception("Not 16x16: "+file);
            var p=new int[256];for(int y=0;y<16;y++)for(int x=0;x<16;x++)p[y*16+x]=b.GetPixel(x,y).ToArgb();return p;
        }
    }
    static bool Solid(int p) { return ((uint)p>>24)!=0; }
    static int Add(string name,int[] pixels,params string[] sources)
    {
        if(cache.TryGetValue(name,out int slot))return slot;
        slot=images.Count;images.Add(pixels);entries.Add(new Entry{Name=name,Slot=slot,Sources=sources});cache.Add(name,slot);return slot;
    }
    static int AddComposite(string name,int[] pixels,params string[] sources)
    {
        var bytes=new byte[pixels.Length*4];Buffer.BlockCopy(pixels,0,bytes,0,bytes.Length);
        string signature=Convert.ToBase64String(bytes);
        if(compositePixels.TryGetValue(signature,out int existing)) { cache[name]=existing;return existing; }
        int slot=Add(name,pixels,sources);compositePixels.Add(signature,slot);return slot;
    }
    static int Normalize(int mask)
    {
        foreach(var c in new[]{new[]{2,1,4},new[]{8,4,16},new[]{32,16,64},new[]{128,64,1}})
            if((mask&c[1])==0||(mask&c[2])==0)mask&=~c[0];return mask;
    }
    static string CanonicalTop(int mask)
    {
        switch(mask) {
            case 255:return "top-center";
            case 124:return "top-N";case 241:return "top-E";case 199:return "top-S";case 31:return "top-W";
            case 28:return "top-outer-NW";case 112:return "top-outer-NE";case 193:return "top-outer-SE";case 7:return "top-outer-SW";
            case 127:return "top-inner-NW";case 253:return "top-inner-NE";case 247:return "top-inner-SE";case 223:return "top-inner-SW";
            default:return null;
        }
    }
    static string QuarterTop(int mask,int x,int y)
    {
        bool north=y<8,west=x<8;int a=north?1:16,b=west?64:4;
        int diag=north?(west?128:2):(west?32:8);
        string corner=(north?"N":"S")+(west?"W":"E");
        bool ca=(mask&a)!=0,cb=(mask&b)!=0;
        if(ca&&cb)return (mask&diag)!=0?"top-center":"top-inner-"+corner;
        if(!ca&&!cb)return "top-outer-"+corner;
        return "top-"+(!ca?(north?"N":"S"):(west?"W":"E"));
    }
    static int Top(int[] states)
    {
        int mask=0,cliffs=0;
        for(int d=0;d<8;d++) { if(states[d]==2)mask|=1<<d;else if(states[d]==1)cliffs|=1<<d; }
        mask=Normalize(mask);
        // A rear edge faces background only when the northern cell is outside;
        // a taller cliff behind it must remain rock, not a hole to the grass.
        bool rearCliff=states[0]==1;
        // Do not collapse cliffs into outside. In particular a west/east cliff
        // needs a solid rock join where the outside variant has a clear column.
        string name="summit_"+mask.ToString("D3")+"_cliffs_"+cliffs.ToString("D3");
        if(cache.TryGetValue(name,out int slot))return slot;
        string canonical=CanonicalTop(mask);var pixels=new int[256];var sources=new HashSet<string>();
        for(int y=0;y<16;y++)for(int x=0;x<16;x++) {
            int i=y*16+x;string id=canonical??QuarterTop(mask,x,y);sources.Add(id);
            int p=masters[id][i];
            if(!Solid(p)) {
                // The approved preview puts a saved wall under the transparent
                // front/side cap, while preserving the rear rim's transparency.
                bool rearCut=!rearCliff&&((mask&1)==0&&!Solid(masters["top-N"][i]));
                bool rearNotch=(id=="top-inner-NW"&&states[7]==0)||(id=="top-inner-NE"&&states[1]==0);
                if(!rearCut&&!rearNotch) {
                    bool left=x<8;int sideBit=left?64:4,frontDiag=left?32:8;
                    string face=(mask&sideBit)==0?(states[left?6:2]==0?(left?"left":"right"):"front"):
                        ((mask&16)!=0&&(mask&frontDiag)==0?(left?"inner-left":"inner-right"):"front");
                    string wall="wall-"+face;sources.Add(wall);p=masters[wall][i];
                }
            }
            pixels[i]=p;
        }
        var sourceArray=new string[sources.Count];sources.CopyTo(sourceArray);Array.Sort(sourceArray);
        return AddComposite(name,pixels,sourceArray);
    }
    static string WallFace(int[] s,bool left)
    {
        int side=left?6:2;
        if(s[side]==0)return left?"left":"right";
        if(s[side]==2)return left?"inner-left":"inner-right";
        // A missing diagonal does not expose this whole cardinal edge. The
        // adjacent cell is still rock: keep the shared face solid and unoutlined.
        return "front";
    }
    static int Face(int part,int[] states)
    {
        string group=part==0?"foot-":"wall-";
        string left=WallFace(states,true),right=WallFace(states,false);
        bool rearEdge=states[0]==0;
        if(!rearEdge) {
            if(left=="front"&&right=="front")return cache[group+"front"];
            if(right=="front")return cache[group+left];if(left=="front")return cache[group+right];
        }
        string name=group+left+"_"+right+(rearEdge?"_rear-clip":"");
        if(cache.TryGetValue(name,out int slot))return slot;
        var p=new int[256];
        for(int y=0;y<16;y++)for(int x=0;x<16;x++) {
            int i=y*16+x;
            p[i]=rearEdge&&!Solid(masters["top-N"][i])?masters["top-N"][i]:masters[group+(x<8?left:right)][i];
        }
        // The cap's clear rear band also clips the exposed cliff crest. Without
        // this the cliff's rectangular dark edge sticks above the adjacent rim.
        return AddComposite(name,p,group+left,group+right,"top-N");
    }
    public static Result Run(string source,string atlas)
    {
        masters=new Dictionary<string,int[]>();images=new List<int[]>();entries=new List<Entry>();cache=new Dictionary<string,int>();compositePixels=new Dictionary<string,int>();
        string[] files=Directory.GetFiles(source,"*.png");Array.Sort(files,StringComparer.Ordinal);
        if(files.Length!=27)throw new Exception("Expected exactly 27 masters.");
        foreach(string file in files){string id=Path.GetFileNameWithoutExtension(file);masters[id]=Read(file);Add(id,masters[id],id);}
        var lookups=new int[3][];
        for(int part=0;part<3;part++) {
            lookups[part]=new int[6561];
            for(int key=0;key<6561;key++) {
                var states=new int[8];int value=key;for(int i=0;i<8;i++){states[i]=value%3;value/=3;}
                lookups[part][key]=part==2?Top(states):Face(part,states);
            }
        }
        int w=256,h=((images.Count+15)/16)*16;
        using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb)) {
            for(int slot=0;slot<images.Count;slot++)for(int y=0;y<16;y++)for(int x=0;x<16;x++)
                b.SetPixel((slot%16)*16+x,(slot/16)*16+y,Color.FromArgb(images[slot][y*16+x]));
            b.Save(atlas,ImageFormat.Png);
        }
        return new Result{Sprites=entries,Lookups=lookups,Width=w,Height=h};
    }
}
