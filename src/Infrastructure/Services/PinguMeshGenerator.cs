using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
using DomainModels = OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Generates Pingu mesh data files (pingu.mesh, pingu.json, pingu.png) in memory.
/// </summary>
public class PinguMeshGenerator
{
    private readonly ILogger<PinguMeshGenerator>? _logger;
    private Random _random;

    private const int TargetVertexCount = 812;
    private const int TargetTriangleCount = 1200;
    private const int AtlasWidth = 512;
    private const int AtlasHeight = 512;

    private static readonly string[] DefaultBoneNames =
    [
        "root", "spine", "chest", "neck", "head",
        "leftArm", "leftForeArm", "leftHand",
        "rightArm", "rightForeArm", "rightHand",
        "leftUpLeg", "leftLeg", "leftFoot",
        "rightUpLeg", "rightLeg", "rightFoot",
        "leftEar", "rightEar",
    ];

    public PinguMeshGenerator(ILogger<PinguMeshGenerator>? logger = null, Random? random = null)
    {
        _logger = logger;
        _random = random ?? new Random();
    }

    public PinguCharacterData Generate(int? seed = null)
    {
        if (seed.HasValue)
            _random = new Random(seed.Value);

        return new PinguCharacterData
        {
            MeshData = GenerateMesh(),
            BoneHierarchy = GenerateBoneHierarchy(),
            AnimationClips = GenerateAnimationClips(),
            PhysicsParams = GeneratePhysicsParams(),
            TextureAtlas = GenerateTextureAtlas(),
            Seed = seed ?? _random.Next(),
        };
    }

    private PinguMeshData GenerateMesh()
    {
        var mesh = new PinguMeshData { AtlasWidth = AtlasWidth, AtlasHeight = AtlasHeight };
        GenerateBody(mesh);
        GenerateHead(mesh);
        GenerateFlippers(mesh);
        GenerateLegs(mesh);
        GenerateBeak(mesh);
        GenerateEyes(mesh);
        while (mesh.VertexCount < TargetVertexCount) AddDetailVertex(mesh);
        while (mesh.TriangleCount < TargetTriangleCount) AddDetailTriangle(mesh);
        NormalizeSkinWeights(mesh);
        ComputeZOrder(mesh);
        return mesh;
    }

    private void GenerateBody(PinguMeshData mesh)
    {
        var bodyWidth = 60;
        var segmentsX = 8;
        var segmentsY = 16;
        for (var y = 0; y < segmentsY; y++)
        {
            for (var x = 0; x < segmentsX; x++)
            {
                var normX = (x / (float)segmentsX - 0.5f) * 2f;
                var normY = (y / (float)segmentsY - 0.5f) * 2f;
                var widthFactor = 1.0f - 0.3f * normY * normY;
                var radius = bodyWidth * widthFactor / 2f;
                for (var cornerY = 0; cornerY < 2; cornerY++)
                {
                    var cy = y + cornerY;
                    var cv = cy / (float)segmentsY;
                    var yNorm = (cy / (float)segmentsY - 0.5f) * 2f;
                    var segRadius = radius * (1.0f - 0.2f * yNorm * yNorm);
                    for (var cornerX = 0; cornerX < 2; cornerX++)
                    {
                        var cx = x + cornerX;
                        var cu = cx / (float)segmentsX;
                        var xNorm = (cx / (float)segmentsX - 0.5f) * 2f;
                        mesh.Vertices.Add(new PinguVertex
                        {
                            X = (float)xNorm * segRadius,
                            Y = (float)yNorm * segRadius * 0.6f,
                            Z = (float)Math.Sqrt(Math.Max(0, segRadius * segRadius - xNorm * xNorm * segRadius * segRadius - yNorm * yNorm * segRadius * segRadius * 0.36f)),
                            NormalX = (float)xNorm,
                            NormalY = (float)yNorm * 0.6f,
                            NormalZ = 0.8f,
                            U = cu, V = cv,
                            BoneIndex0 = 1, BoneIndex1 = 2, BoneIndex2 = 0, BoneIndex3 = 0,
                            BoneWeight0 = 0.6f, BoneWeight1 = 0.3f, BoneWeight2 = 0.1f, BoneWeight3 = 0.0f,
                        });
                    }
                }
            }
        }
    }

    private void GenerateHead(PinguMeshData mesh)
    {
        var headRadius = 35;
        var segments = 12;
        for (var i = 0; i < segments; i++)
        {
            var angle1 = (i / (float)segments) * Math.PI * 2;
            var angle2 = ((i + 1) / (float)segments) * Math.PI * 2;
            var x1 = (float)Math.Cos(angle1) * headRadius;
            var y1 = (float)Math.Sin(angle1) * headRadius;
            var z1 = (float)Math.Sqrt(Math.Max(0, headRadius * headRadius - x1 * x1 - y1 * y1));
            var x2 = (float)Math.Cos(angle2) * headRadius;
            var y2 = (float)Math.Sin(angle2) * headRadius;
            var z2 = (float)Math.Sqrt(Math.Max(0, headRadius * headRadius - x2 * x2 - y2 * y2));
            mesh.Vertices.Add(new PinguVertex { X = x1, Y = y1 + 130, Z = z1, NormalX = (float)Math.Cos(angle1), NormalY = (float)Math.Sin(angle1), NormalZ = z1 / headRadius, U = (float)(angle1 / (Math.PI * 2)), V = 0.1f, BoneIndex0 = 4, BoneIndex1 = 3, BoneWeight0 = 0.7f, BoneWeight1 = 0.3f });
            mesh.Vertices.Add(new PinguVertex { X = x2, Y = y2 + 130, Z = z2, NormalX = (float)Math.Cos(angle2), NormalY = (float)Math.Sin(angle2), NormalZ = z2 / headRadius, U = (float)(angle2 / (Math.PI * 2)), V = 0.1f, BoneIndex0 = 4, BoneIndex1 = 3, BoneWeight0 = 0.7f, BoneWeight1 = 0.3f });
            mesh.Vertices.Add(new PinguVertex { X = x1 * 0.5f, Y = y1 * 0.5f + 130, Z = z1 * 0.5f, NormalX = (float)Math.Cos(angle1), NormalY = (float)Math.Sin(angle1), NormalZ = z1 / headRadius, U = (float)(angle1 / (Math.PI * 2)), V = 0.3f, BoneIndex0 = 4, BoneIndex1 = 3, BoneWeight0 = 0.7f, BoneWeight1 = 0.3f });
            mesh.Vertices.Add(new PinguVertex { X = x2 * 0.5f, Y = y2 * 0.5f + 130, Z = z2 * 0.5f, NormalX = (float)Math.Cos(angle2), NormalY = (float)Math.Sin(angle2), NormalZ = z2 / headRadius, U = (float)(angle2 / (Math.PI * 2)), V = 0.3f, BoneIndex0 = 4, BoneIndex1 = 3, BoneWeight0 = 0.7f, BoneWeight1 = 0.3f });
        }
    }

    private void GenerateFlippers(PinguMeshData mesh)
    {
        for (var side = -1; side <= 1; side += 2)
        {
            var baseX = side * 65;
            var boneBase = side == -1 ? 5 : 8;
            var boneFore = side == -1 ? 6 : 9;
            for (var i = 0; i < 6; i++)
            {
                var t = i / 6f;
                var width = 15f * (1f - t);
                mesh.Vertices.Add(new PinguVertex { X = baseX - side * width, Y = 40 + t * 60, Z = width * 0.3f, NormalX = -side, NormalY = t, NormalZ = 0.5f, U = (float)(0.1f + t * 0.2f), V = 0.5f, BoneIndex0 = (byte)boneBase, BoneIndex1 = (byte)boneFore, BoneWeight0 = 0.7f, BoneWeight1 = 0.3f });
                mesh.Vertices.Add(new PinguVertex { X = baseX + side * width, Y = 40 + t * 60, Z = -width * 0.3f, NormalX = side, NormalY = t, NormalZ = -0.5f, U = (float)(0.3f + t * 0.2f), V = 0.5f, BoneIndex0 = (byte)boneBase, BoneIndex1 = (byte)boneFore, BoneWeight0 = 0.7f, BoneWeight1 = 0.3f });
            }
        }
    }

    private void GenerateLegs(PinguMeshData mesh)
    {
        for (var side = -1; side <= 1; side += 2)
        {
            var boneBase = side == -1 ? 12 : 15;
            var boneFore = side == -1 ? 13 : 16;
            for (var i = 0; i < 4; i++)
            {
                var t = i / 4f;
                mesh.Vertices.Add(new PinguVertex { X = side * 20, Y = -80 - t * 40, Z = 10, NormalX = 0, NormalY = -1, NormalZ = 0.5f, U = 0.1f, V = 0.7f + t * 0.1f, BoneIndex0 = (byte)boneBase, BoneIndex1 = (byte)boneFore, BoneWeight0 = 0.7f, BoneWeight1 = 0.3f });
                mesh.Vertices.Add(new PinguVertex { X = side * 30, Y = -80 - t * 40, Z = 5, NormalX = -side, NormalY = 0, NormalZ = 0.3f, U = 0.15f, V = 0.7f + t * 0.1f, BoneIndex0 = (byte)boneBase, BoneIndex1 = (byte)boneFore, BoneWeight0 = 0.7f, BoneWeight1 = 0.3f });
            }
        }
    }

    private void GenerateBeak(PinguMeshData mesh)
    {
        var beakSegments = 6;
        for (var i = 0; i < beakSegments; i++)
        {
            var t = i / (float)beakSegments;
            var width = 8f * (1f - t * 0.5f);
            var y = 135 + t * 25;
            mesh.Vertices.Add(new PinguVertex { X = -width, Y = y, Z = 5, NormalX = -0.5f, NormalY = 0.2f, NormalZ = 0.8f, U = 0.05f, V = 0.05f + t * 0.1f, BoneIndex0 = 4, BoneIndex1 = 0, BoneWeight0 = 0.8f, BoneWeight1 = 0.2f });
            mesh.Vertices.Add(new PinguVertex { X = width, Y = y, Z = 5, NormalX = 0.5f, NormalY = 0.2f, NormalZ = 0.8f, U = 0.15f, V = 0.05f + t * 0.1f, BoneIndex0 = 4, BoneIndex1 = 0, BoneWeight0 = 0.8f, BoneWeight1 = 0.2f });
        }
    }

    private void GenerateEyes(PinguMeshData mesh)
    {
        mesh.Vertices.Add(new PinguVertex { X = -15, Y = 140, Z = 30, NormalX = -0.3f, NormalY = 0.1f, NormalZ = 0.95f, U = 0.2f, V = 0.05f, BoneIndex0 = 4, BoneWeight0 = 1f });
        mesh.Vertices.Add(new PinguVertex { X = -5, Y = 140, Z = 32, NormalX = 0.1f, NormalY = 0.1f, NormalZ = 0.98f, U = 0.25f, V = 0.05f, BoneIndex0 = 4, BoneWeight0 = 1f });
        mesh.Vertices.Add(new PinguVertex { X = -10, Y = 150, Z = 28, NormalX = -0.2f, NormalY = 0.5f, NormalZ = 0.85f, U = 0.22f, V = 0.1f, BoneIndex0 = 4, BoneWeight0 = 1f });
        mesh.Vertices.Add(new PinguVertex { X = 15, Y = 140, Z = 30, NormalX = 0.3f, NormalY = 0.1f, NormalZ = 0.95f, U = 0.75f, V = 0.05f, BoneIndex0 = 4, BoneWeight0 = 1f });
        mesh.Vertices.Add(new PinguVertex { X = 5, Y = 140, Z = 32, NormalX = -0.1f, NormalY = 0.1f, NormalZ = 0.98f, U = 0.7f, V = 0.05f, BoneIndex0 = 4, BoneWeight0 = 1f });
        mesh.Vertices.Add(new PinguVertex { X = 10, Y = 150, Z = 28, NormalX = 0.2f, NormalY = 0.5f, NormalZ = 0.85f, U = 0.78f, V = 0.1f, BoneIndex0 = 4, BoneWeight0 = 1f });
    }

    private void AddDetailVertex(PinguMeshData mesh)
    {
        var idx = _random.Next(mesh.Vertices.Count - 1);
        var baseV = mesh.Vertices[idx];
        mesh.Vertices.Add(new PinguVertex
        {
            X = baseV.X + (float)(_random.NextDouble() - 0.5) * 5f,
            Y = baseV.Y + (float)(_random.NextDouble() - 0.5) * 5f,
            Z = baseV.Z + (float)(_random.NextDouble() - 0.5) * 3f,
            NormalX = baseV.NormalX, NormalY = baseV.NormalY, NormalZ = baseV.NormalZ,
            U = baseV.U + (float)(_random.NextDouble() - 0.5) * 0.05f,
            V = baseV.V + (float)(_random.NextDouble() - 0.5) * 0.05f,
            BoneIndex0 = baseV.BoneIndex0, BoneIndex1 = baseV.BoneIndex1,
            BoneIndex2 = baseV.BoneIndex2, BoneIndex3 = baseV.BoneIndex3,
            BoneWeight0 = baseV.BoneWeight0, BoneWeight1 = baseV.BoneWeight1,
            BoneWeight2 = baseV.BoneWeight2, BoneWeight3 = baseV.BoneWeight3,
        });
    }

    private void AddDetailTriangle(PinguMeshData mesh)
    {
        var count = mesh.Vertices.Count;
        mesh.Triangles.Add(new PinguTriangle
        {
            Vertex0 = _random.Next(count),
            Vertex1 = _random.Next(count),
            Vertex2 = _random.Next(count),
        });
    }

    private void NormalizeSkinWeights(PinguMeshData mesh)
    {
        foreach (var vertex in mesh.Vertices)
        {
            var total = vertex.BoneWeight0 + vertex.BoneWeight1 + vertex.BoneWeight2 + vertex.BoneWeight3;
            if (total > 0 && Math.Abs(total - 1.0f) > 0.001f)
            {
                vertex.BoneWeight0 /= total;
                vertex.BoneWeight1 /= total;
                vertex.BoneWeight2 /= total;
                vertex.BoneWeight3 /= total;
            }
        }
    }

    private void ComputeZOrder(PinguMeshData mesh)
    {
        foreach (var tri in mesh.Triangles)
        {
            var avgZ = (mesh.Vertices[tri.Vertex0].Z + mesh.Vertices[tri.Vertex1].Z + mesh.Vertices[tri.Vertex2].Z) / 3f;
            tri.ZOrder = avgZ;
        }
    }

    private PinguBoneHierarchy GenerateBoneHierarchy()
    {
        var definitions = new List<PinguBoneDefinition>
        {
            new() { Name = "root", Index = 0, X = 0, Y = -100, Z = 0 },
            new() { Name = "spine", Index = 1, ParentIndex = 0, X = 0, Y = -60, Z = 0 },
            new() { Name = "chest", Index = 2, ParentIndex = 1, X = 0, Y = -40, Z = 0 },
            new() { Name = "neck", Index = 3, ParentIndex = 2, X = 0, Y = -30, Z = 0 },
            new() { Name = "head", Index = 4, ParentIndex = 3, X = 0, Y = -25, Z = 0 },
            new() { Name = "leftArm", Index = 5, ParentIndex = 2, X = -30, Y = -10, Z = 0, MinRoll = -90, MaxRoll = 45 },
            new() { Name = "leftForeArm", Index = 6, ParentIndex = 5, X = -25, Y = 0, Z = 0 },
            new() { Name = "leftHand", Index = 7, ParentIndex = 6, X = -20, Y = 0, Z = 0 },
            new() { Name = "rightArm", Index = 8, ParentIndex = 2, X = 30, Y = -10, Z = 0, MinRoll = -45, MaxRoll = 90 },
            new() { Name = "rightForeArm", Index = 9, ParentIndex = 8, X = 25, Y = 0, Z = 0 },
            new() { Name = "rightHand", Index = 10, ParentIndex = 9, X = 20, Y = 0, Z = 0 },
            new() { Name = "leftUpLeg", Index = 11, ParentIndex = 0, X = -15, Y = 0, Z = 0 },
            new() { Name = "leftLeg", Index = 12, ParentIndex = 11, X = 0, Y = -35, Z = 0 },
            new() { Name = "leftFoot", Index = 13, ParentIndex = 12, X = 0, Y = -30, Z = 10 },
            new() { Name = "rightUpLeg", Index = 14, ParentIndex = 0, X = 15, Y = 0, Z = 0 },
            new() { Name = "rightLeg", Index = 15, ParentIndex = 14, X = 0, Y = -35, Z = 0 },
            new() { Name = "rightFoot", Index = 16, ParentIndex = 15, X = 0, Y = -30, Z = 10 },
            new() { Name = "leftEar", Index = 17, ParentIndex = 4, X = -15, Y = 20, Z = 10 },
            new() { Name = "rightEar", Index = 18, ParentIndex = 4, X = 15, Y = 20, Z = 10 },
        };

        var hierarchy = new PinguBoneHierarchy { Definitions = definitions };
        hierarchy.Resolve();
        return hierarchy;
    }

    private List<DomainModels.PinguAnimationClip> GenerateAnimationClips()
    {
        return new List<DomainModels.PinguAnimationClip>
        {
            new() { Name = "Idle", Duration = 2.0f, Looping = true, Tracks = GenerateIdleTracks() },
            new() { Name = "Walk", Duration = 0.8f, Looping = true, Tracks = GenerateWalkTracks() },
            new() { Name = "Sit", Duration = 1.5f, Looping = false, Tracks = GenerateSitTracks() },
            new() { Name = "Wave", Duration = 1.0f, Looping = true, Tracks = GenerateWaveTracks() },
            new() { Name = "Twitch", Duration = 0.3f, Looping = false, Tracks = GenerateTwitchTracks() },
        };
    }

    private List<DomainModels.BoneAnimationTrack> GenerateIdleTracks()
    {
        var tracks = new List<DomainModels.BoneAnimationTrack>();
        foreach (var bone in DefaultBoneNames)
        {
            tracks.Add(new DomainModels.BoneAnimationTrack
            {
                BoneIndex = Array.IndexOf(DefaultBoneNames, bone),
                Keyframes =
                [
                    new() { Time = 0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 0.5f, Roll = (float)(_random.NextDouble() * 4 - 2), Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 1.0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 1.5f, Roll = (float)(_random.NextDouble() * 4 - 2), Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 2.0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                ],
            });
        }
        return tracks;
    }

    private List<DomainModels.BoneAnimationTrack> GenerateWalkTracks()
    {
        var tracks = new List<DomainModels.BoneAnimationTrack>();
        foreach (var bone in DefaultBoneNames)
        {
            tracks.Add(new DomainModels.BoneAnimationTrack
            {
                BoneIndex = Array.IndexOf(DefaultBoneNames, bone),
                Keyframes =
                [
                    new() { Time = 0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 0.4f, Roll = (float)(_random.NextDouble() * 10 - 5), Pitch = (float)(_random.NextDouble() * 10 - 5), Yaw = 0, Scale = 1f },
                    new() { Time = 0.8f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                ],
            });
        }
        return tracks;
    }

    private List<DomainModels.BoneAnimationTrack> GenerateSitTracks()
    {
        var tracks = new List<DomainModels.BoneAnimationTrack>();
        foreach (var bone in DefaultBoneNames)
        {
            tracks.Add(new DomainModels.BoneAnimationTrack
            {
                BoneIndex = Array.IndexOf(DefaultBoneNames, bone),
                Keyframes =
                [
                    new() { Time = 0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 0.75f, Roll = 0, Pitch = 30, Yaw = 0, Scale = 1f },
                    new() { Time = 1.5f, Roll = 0, Pitch = 30, Yaw = 0, Scale = 1f },
                ],
            });
        }
        return tracks;
    }

    private List<DomainModels.BoneAnimationTrack> GenerateWaveTracks()
    {
        var tracks = new List<DomainModels.BoneAnimationTrack>();
        foreach (var bone in DefaultBoneNames)
        {
            var track = new DomainModels.BoneAnimationTrack { BoneIndex = Array.IndexOf(DefaultBoneNames, bone) };
            if (bone == "rightArm" || bone == "rightForeArm")
            {
                track.Keyframes =
                [
                    new() { Time = 0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 0.25f, Roll = -45, Pitch = 10, Yaw = 0, Scale = 1f },
                    new() { Time = 0.5f, Roll = 45, Pitch = 10, Yaw = 0, Scale = 1f },
                    new() { Time = 0.75f, Roll = -45, Pitch = 10, Yaw = 0, Scale = 1f },
                    new() { Time = 1.0f, Roll = 45, Pitch = 10, Yaw = 0, Scale = 1f },
                ];
            }
            else
            {
                track.Keyframes =
                [
                    new() { Time = 0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 1.0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                ];
            }
            tracks.Add(track);
        }
        return tracks;
    }

    private List<DomainModels.BoneAnimationTrack> GenerateTwitchTracks()
    {
        var tracks = new List<DomainModels.BoneAnimationTrack>();
        foreach (var bone in DefaultBoneNames)
        {
            tracks.Add(new DomainModels.BoneAnimationTrack
            {
                BoneIndex = Array.IndexOf(DefaultBoneNames, bone),
                Keyframes =
                [
                    new() { Time = 0f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                    new() { Time = 0.15f, Roll = (float)(_random.NextDouble() * 20 - 10), Pitch = (float)(_random.NextDouble() * 10 - 5), Yaw = 0, Scale = 1f },
                    new() { Time = 0.3f, Roll = 0, Pitch = 0, Yaw = 0, Scale = 1f },
                ],
            });
        }
        return tracks;
    }

    private PinguPhysicsParams GeneratePhysicsParams()
    {
        return new PinguPhysicsParams
        {
            Mass = 1.0f, Friction = 0.6f, Gravity = 980.0f,
            IkStiffness = 0.7f, VelocityDamping = 0.95f,
            SpringStiffness = 0.1f, SpringRestLength = 1.0f,
            MaxWalkSpeed = 100.0f, MaxRunSpeed = 200.0f,
            Acceleration = 300.0f, Deceleration = 400.0f,
        };
    }

    private byte[] GenerateTextureAtlas()
    {
        var info = new SkiaSharp.SKImageInfo(AtlasWidth, AtlasHeight);
        using var surface = SkiaSharp.SKSurface.Create(info);
        using var canvas = surface.Canvas;
        var paint = new SkiaSharp.SKPaint { IsAntialias = true, IsStroke = false };

        paint.Color = new SkiaSharp.SKColor(240, 240, 245);
        canvas.DrawRect(new SkiaSharp.SKRect(0, 0, AtlasWidth, AtlasHeight), paint);
        paint.Color = new SkiaSharp.SKColor(240, 240, 245);
        canvas.DrawOval(new SkiaSharp.SKRect(100, 80, 412, 400), paint);
        paint.Color = new SkiaSharp.SKColor(30, 30, 40);
        canvas.DrawOval(new SkiaSharp.SKRect(180, 40, 332, 192), paint);
        paint.Color = new SkiaSharp.SKColor(255, 255, 255);
        canvas.DrawOval(new SkiaSharp.SKRect(210, 70, 240, 100), paint);
        canvas.DrawOval(new SkiaSharp.SKRect(272, 70, 302, 100), paint);
        paint.Color = new SkiaSharp.SKColor(10, 10, 10);
        canvas.DrawOval(new SkiaSharp.SKRect(220, 80, 232, 92), paint);
        canvas.DrawOval(new SkiaSharp.SKRect(282, 80, 294, 92), paint);
        paint.Color = new SkiaSharp.SKColor(255, 165, 0);
        canvas.DrawOval(new SkiaSharp.SKRect(230, 105, 282, 130), paint);
        paint.Color = new SkiaSharp.SKColor(245, 245, 250);
        canvas.DrawOval(new SkiaSharp.SKRect(160, 160, 352, 360), paint);
        paint.Color = new SkiaSharp.SKColor(255, 165, 0);
        canvas.DrawOval(new SkiaSharp.SKRect(130, 380, 200, 420), paint);
        canvas.DrawOval(new SkiaSharp.SKRect(312, 380, 382, 420), paint);

        paint.Color = new SkiaSharp.SKColor(20, 20, 30);
        paint.StrokeWidth = 2;
        paint.IsStroke = true;
        canvas.DrawOval(new SkiaSharp.SKRect(60, 120, 130, 260), paint);
        canvas.DrawOval(new SkiaSharp.SKRect(382, 120, 452, 260), paint);
        paint.IsStroke = false;

        return surface.Snapshot().Encode(SkiaSharp.SKEncodedImageFormat.Png, 90).ToArray();
    }
}