#region Assembly SkiaSharp, Version=3.116.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756
// C:\Users\Yonneh\.nuget\packages\skiasharp\3.116.1\ref\net8.0\SkiaSharp.dll
// Decompiled with ICSharpCode.Decompiler 9.1.0.7988
#endregion

using System;

namespace SkiaSharp;

public struct SKCodecFrameInfo : IEquatable<SKCodecFrameInfo>
{
    private int fRequiredFrame;

    private int fDuration;

    private byte fFullyReceived;

    private SKAlphaType fAlphaType;

    private byte fHasAlphaWithinBounds;

    private SKCodecAnimationDisposalMethod fDisposalMethod;

    private SKCodecAnimationBlend fBlend;

    private SKRectI fFrameRect;

    public int RequiredFrame
    {
        readonly get
        {
            return fRequiredFrame;
        }
        set
        {
            fRequiredFrame = value;
        }
    }

    public int Duration
    {
        readonly get
        {
            return fDuration;
        }
        set
        {
            fDuration = value;
        }
    }

    public bool FullyRecieved
    {
        readonly get
        {
            return fFullyReceived > 0;
        }
        set
        {
            fFullyReceived = (value ? ((byte)1) : ((byte)0));
        }
    }

    public SKAlphaType AlphaType
    {
        readonly get
        {
            return fAlphaType;
        }
        set
        {
            fAlphaType = value;
        }
    }

    public bool HasAlphaWithinBounds
    {
        readonly get
        {
            return fHasAlphaWithinBounds > 0;
        }
        set
        {
            fHasAlphaWithinBounds = (value ? ((byte)1) : ((byte)0));
        }
    }

    public SKCodecAnimationDisposalMethod DisposalMethod
    {
        readonly get
        {
            return fDisposalMethod;
        }
        set
        {
            fDisposalMethod = value;
        }
    }

    public SKCodecAnimationBlend Blend
    {
        readonly get
        {
            return fBlend;
        }
        set
        {
            fBlend = value;
        }
    }

    public SKRectI FrameRect
    {
        readonly get
        {
            return fFrameRect;
        }
        set
        {
            fFrameRect = value;
        }
    }

    public readonly bool Equals(SKCodecFrameInfo obj)
    {
        if (fRequiredFrame == obj.fRequiredFrame && fDuration == obj.fDuration && fFullyReceived == obj.fFullyReceived && fAlphaType == obj.fAlphaType && fHasAlphaWithinBounds == obj.fHasAlphaWithinBounds && fDisposalMethod == obj.fDisposalMethod && fBlend == obj.fBlend)
        {
            return fFrameRect == obj.fFrameRect;
        }

        return false;
    }

    public override readonly bool Equals(object obj)
    {
        if (obj is SKCodecFrameInfo obj2)
        {
            return Equals(obj2);
        }

        return false;
    }

    public static bool operator ==(SKCodecFrameInfo left, SKCodecFrameInfo right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SKCodecFrameInfo left, SKCodecFrameInfo right)
    {
        return !left.Equals(right);
    }

    public override readonly int GetHashCode()
    {
        HashCode hashCode = default(HashCode);
        hashCode.Add(fRequiredFrame);
        hashCode.Add(fDuration);
        hashCode.Add(fFullyReceived);
        hashCode.Add(fAlphaType);
        hashCode.Add(fHasAlphaWithinBounds);
        hashCode.Add(fDisposalMethod);
        hashCode.Add(fBlend);
        hashCode.Add(fFrameRect);
        return hashCode.ToHashCode();
    }
}
#if false // Decompilation log
'317' items in cache
------------------
Resolve: 'System.Runtime, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Runtime, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.26\ref\net8.0\System.Runtime.dll'
------------------
Resolve: 'System.Runtime.InteropServices, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Runtime.InteropServices, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.26\ref\net8.0\System.Runtime.InteropServices.dll'
------------------
Resolve: 'System.Collections, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Collections, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.26\ref\net8.0\System.Collections.dll'
------------------
Resolve: 'System.Numerics.Vectors, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Numerics.Vectors, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.26\ref\net8.0\System.Numerics.Vectors.dll'
------------------
Resolve: 'System.Collections.Concurrent, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Collections.Concurrent, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.26\ref\net8.0\System.Collections.Concurrent.dll'
------------------
Resolve: 'System.Threading, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Threading, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.26\ref\net8.0\System.Threading.dll'
------------------
Resolve: 'System.Memory, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Found single assembly: 'System.Memory, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.26\ref\net8.0\System.Memory.dll'
------------------
Resolve: 'System.Linq, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Linq, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.26\ref\net8.0\System.Linq.dll'
------------------
Resolve: 'System.Runtime.CompilerServices.Unsafe, Version=8.0.0.0, Culture=neutral, PublicKeyToken=null'
Found single assembly: 'System.Runtime.CompilerServices.Unsafe, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.26\ref\net8.0\System.Runtime.CompilerServices.Unsafe.dll'
#endif
