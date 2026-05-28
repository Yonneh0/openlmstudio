using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Domain.Models;
namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Consolidated Pingu system containing:
/// - PinguMeshGenerator: Mesh, bone hierarchy, animation clips, texture atlas generation
/// - PinguBoneLoader: JSON serialization for bones, animations, physics
/// - PinguAnimationStateMachine: State machine with blending between animation states
/// - PinguBehaviorTriggers: Pseudo-random behaviors (twitch, head turn, etc.)
/// - PinguInverseKinematics: CCD IK solver for limbs
/// - PinguAnimationSystem: Real-time animation & physics solver (IK, bone transforms)
/// - PinguPhysicsSolver: Distance constraints, velocity damping, gravity
/// - PinguToolHolder: Tool attachment system
/// - PinguNPCManager: NPC character management, tasks, roles, movement
/// - PinguSystemPrompts: Static system prompts
/// - PinguService: Orchestrator wrapping all subsystems
/// </summary>

#region PinguMeshGenerator

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

        var mesh = GenerateMesh();
        var textureAtlas = GenerateTextureAtlas();

        ApplyTextureAtlasToMesh(mesh, textureAtlas);

        return new PinguCharacterData
        {
            MeshData = mesh,
            BoneHierarchy = GenerateBoneHierarchy(),
            AnimationClips = GenerateAnimationClips(),
            PhysicsParams = GeneratePhysicsParams(),
            TextureAtlas = textureAtlas,
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
                            U = cu,
                            V = cv,
                            BoneIndex0 = 1,
                            BoneIndex1 = 2,
                            BoneIndex2 = 0,
                            BoneIndex3 = 0,
                            BoneWeight0 = 0.6f,
                            BoneWeight1 = 0.3f,
                            BoneWeight2 = 0.1f,
                            BoneWeight3 = 0.0f,
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
            NormalX = baseV.NormalX,
            NormalY = baseV.NormalY,
            NormalZ = baseV.NormalZ,
            U = baseV.U + (float)(_random.NextDouble() - 0.5) * 0.05f,
            V = baseV.V + (float)(_random.NextDouble() - 0.5) * 0.05f,
            BoneIndex0 = baseV.BoneIndex0,
            BoneIndex1 = baseV.BoneIndex1,
            BoneIndex2 = baseV.BoneIndex2,
            BoneIndex3 = baseV.BoneIndex3,
            BoneWeight0 = baseV.BoneWeight0,
            BoneWeight1 = baseV.BoneWeight1,
            BoneWeight2 = baseV.BoneWeight2,
            BoneWeight3 = baseV.BoneWeight3,
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

    private void ApplyTextureAtlasToMesh(PinguMeshData mesh, byte[] textureAtlas)
    {
        mesh.TextureAtlasBytes = textureAtlas;
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

    private List<PinguAnimationClip> GenerateAnimationClips()
    {
        return new List<PinguAnimationClip>
        {
            new() { Name = "Idle", Duration = 2.0f, Looping = true, Tracks = GenerateIdleTracks() },
            new() { Name = "Walk", Duration = 0.8f, Looping = true, Tracks = GenerateWalkTracks() },
            new() { Name = "Sit", Duration = 1.5f, Looping = false, Tracks = GenerateSitTracks() },
            new() { Name = "Wave", Duration = 1.0f, Looping = true, Tracks = GenerateWaveTracks() },
            new() { Name = "Twitch", Duration = 0.3f, Looping = false, Tracks = GenerateTwitchTracks() },
        };
    }

    private List<BoneAnimationTrack> GenerateIdleTracks()
    {
        var tracks = new List<BoneAnimationTrack>();
        foreach (var bone in DefaultBoneNames)
        {
            tracks.Add(new BoneAnimationTrack
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

    private List<BoneAnimationTrack> GenerateWalkTracks()
    {
        var tracks = new List<BoneAnimationTrack>();
        foreach (var bone in DefaultBoneNames)
        {
            tracks.Add(new BoneAnimationTrack
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

    private List<BoneAnimationTrack> GenerateSitTracks()
    {
        var tracks = new List<BoneAnimationTrack>();
        foreach (var bone in DefaultBoneNames)
        {
            tracks.Add(new BoneAnimationTrack
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

    private List<BoneAnimationTrack> GenerateWaveTracks()
    {
        var tracks = new List<BoneAnimationTrack>();
        foreach (var bone in DefaultBoneNames)
        {
            var track = new BoneAnimationTrack { BoneIndex = Array.IndexOf(DefaultBoneNames, bone) };
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

    private List<BoneAnimationTrack> GenerateTwitchTracks()
    {
        var tracks = new List<BoneAnimationTrack>();
        foreach (var bone in DefaultBoneNames)
        {
            tracks.Add(new BoneAnimationTrack
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
            Mass = 1.0f,
            Friction = 0.6f,
            Gravity = 980.0f,
            IkStiffness = 0.7f,
            VelocityDamping = 0.95f,
            SpringStiffness = 0.1f,
            SpringRestLength = 1.0f,
            MaxWalkSpeed = 100.0f,
            MaxRunSpeed = 200.0f,
            Acceleration = 300.0f,
            Deceleration = 400.0f,
        };
    }

    /// <summary>
    /// Generate a 512×512 RGBA texture atlas with penguin face and body textures.
    /// </summary>
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

#endregion

#region PinguBoneLoader

/// <summary>
/// Loads and saves Pingu bone hierarchy and animation data from/to JSON.
/// </summary>
public class PinguBoneLoader
{
    private readonly ILogger<PinguBoneLoader>? _logger;

    /// <summary>
    /// Cached JSON serialization options for consistent formatting.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public PinguBoneLoader(ILogger<PinguBoneLoader>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Load bone hierarchy from JSON.
    /// </summary>
    public PinguBoneHierarchy LoadBoneHierarchy(string json)
    {
        var definitions = JsonSerializer.Deserialize<List<PinguBoneDefinition>>(json);
        if (definitions == null)
            throw new ArgumentException("Invalid bone hierarchy JSON");

        var hierarchy = new PinguBoneHierarchy { Definitions = definitions };
        hierarchy.Resolve();
        return hierarchy;
    }

    /// <summary>
    /// Save bone hierarchy to JSON.
    /// </summary>
    public string SaveBoneHierarchy(PinguBoneHierarchy hierarchy)
    {
        return JsonSerializer.Serialize(hierarchy.Definitions, _jsonOptions);
    }

    /// <summary>
    /// Load animation clips from JSON.
    /// </summary>
    public List<PinguAnimationClip> LoadAnimationClips(string json)
    {
        return JsonSerializer.Deserialize<List<PinguAnimationClip>>(json) ?? new List<PinguAnimationClip>();
    }

    /// <summary>
    /// Save animation clips to JSON.
    /// </summary>
    public string SaveAnimationClips(List<PinguAnimationClip> clips)
    {
        return JsonSerializer.Serialize(clips, _jsonOptions);
    }

    /// <summary>
    /// Load physics parameters from JSON.
    /// </summary>
    public PinguPhysicsParams LoadPhysicsParams(string json)
    {
        return JsonSerializer.Deserialize<PinguPhysicsParams>(json) ?? new PinguPhysicsParams();
    }

    /// <summary>
    /// Save physics parameters to JSON.
    /// </summary>
    public string SavePhysicsParams(PinguPhysicsParams paramsData)
    {
        return JsonSerializer.Serialize(paramsData, _jsonOptions);
    }

    /// <summary>
    /// Load a PinguHomeScene with default objects.
    /// </summary>
    public PinguHomeScene LoadHomeScene(string? boneHierarchyJson = null)
    {
        var scene = new PinguHomeScene
        {
            Name = "Default Home",
            BackgroundColor = "#1E1E22",
            Width = 200f,
            Height = 200f,
            Depth = 100f,
            Objects = new List<PinguHomeObject>
            {
                PinguHomeObject.CreateDefaultIgloo(),
                PinguHomeObject.CreateDefaultSink(),
                PinguHomeObject.CreateDefaultRug(),
                PinguHomeObject.CreateDefaultBall(),
                PinguHomeObject.CreateDefaultFishBowl(),
                PinguHomeObject.CreateDefaultNest(),
            }
        };

        // If boneHierarchyJson is provided, parse and apply it
        if (!string.IsNullOrWhiteSpace(boneHierarchyJson))
        {
            try
            {
                var definitions = System.Text.Json.JsonSerializer.Deserialize<List<PinguBoneDefinition>>(boneHierarchyJson);
                if (definitions != null && definitions.Count > 0)
                {
                    scene.BoneHierarchyJson = boneHierarchyJson;

                    // Use the bone hierarchy to customize the scene
                    var rootBone = definitions.FirstOrDefault(d => d.ParentIndex == null);
                    if (rootBone != null)
                    {
                        // Position the scene based on root bone position
                        scene.X = rootBone.X / 200f;
                        scene.Y = rootBone.Y / 200f;
                    }
                }
            }
            catch
            {
                // If JSON is invalid, just use the default scene
            }
        }

        return scene;
    }
}

#endregion

#region PinguAnimationStateMachine

/// <summary>
/// Animation state machine for Pingu characters with seamless blending between states.
/// Supports concurrent animations without overlap conflicts.
/// </summary>
public class PinguAnimationStateMachine
{
    private readonly ILogger<PinguAnimationStateMachine>? _logger;
    private readonly Random _random;
    private readonly List<PinguAnimationClip> _clips;

    /// <summary>
    /// Current active animation state.
    /// </summary>
    public PinguAnimationState CurrentState { get; private set; }

    /// <summary>
    /// Blend factor between old and new clip (0.0 = old, 1.0 = new).
    /// </summary>
    public float BlendFactor { get; private set; }

    /// <summary>
    /// Duration of the blend transition.
    /// </summary>
    public float BlendDuration { get; set; } = 0.3f;

    /// <summary>
    /// Whether a blend is currently in progress.
    /// </summary>
    public bool IsBlending => BlendFactor > 0 && BlendFactor < 1;

    /// <summary>
    /// Current animation clip.
    /// </summary>
    public PinguAnimationClip? CurrentClip { get; private set; }

    /// <summary>
    /// Previous animation clip (used during blending transitions).
    /// </summary>
    private PinguAnimationClip? _previousClip;

    /// <summary>
    /// Cached lookup from PinguAnimationState enum value to animation clip.
    /// </summary>
    private readonly Dictionary<PinguAnimationState, PinguAnimationClip?> _clipByName;

    /// <summary>
    /// Time since last state change.
    /// </summary>
    private float _stateTimer;

    /// <summary>
    /// Weighted behavior triggers for random behaviors.
    /// </summary>
    private readonly List<(PinguAnimationState State, float Weight)> _behaviorTriggers;

    /// <summary>
    /// Priority levels for animations (higher = more important).
    /// </summary>
    private readonly Dictionary<PinguAnimationState, int> _animationPriorities;

    /// <summary>
    /// Whether the animation is currently paused.
    /// </summary>
    private bool _isPaused;

    public PinguAnimationStateMachine(
        List<PinguAnimationClip> clips,
        ILogger<PinguAnimationStateMachine>? logger = null,
        Random? random = null)
    {
        _clips = clips;
        _logger = logger;
        _random = random ?? new Random();

        CurrentState = PinguAnimationState.Idle;
        _stateTimer = 0;
        BlendFactor = 1;

        // Build clip lookup by name (handles clips named differently from enum values)
        _clipByName = new Dictionary<PinguAnimationState, PinguAnimationClip?>();
        foreach (var clip in clips)
        {
            if (Enum.IsDefined(typeof(PinguAnimationState), clip.Name))
            {
                _clipByName[(PinguAnimationState)Enum.Parse(typeof(PinguAnimationState), clip.Name)] = clip;
            }
        }
        // Also add all clips as a fallback
        foreach (var clip in clips)
        {
            if (!_clipByName.Values.Contains(clip))
            {
                // If clip name doesn't match an enum value, add it as the first available fallback
                var fallbackState = _clipByName.Keys.FirstOrDefault(s => _clipByName[s] == null);
                if (fallbackState != default)
                    _clipByName[fallbackState] = clip;
            }
        }

        _animationPriorities = new Dictionary<PinguAnimationState, int>
        {
            [PinguAnimationState.Idle] = 1,
            [PinguAnimationState.Breathing] = 2,
            [PinguAnimationState.Walk] = 3,
            [PinguAnimationState.Run] = 4,
            [PinguAnimationState.Sit] = 3,
            [PinguAnimationState.SittingDown] = 8,
            [PinguAnimationState.SittingUp] = 8,
            [PinguAnimationState.Wave] = 4,
            [PinguAnimationState.Scratch] = 5,
            [PinguAnimationState.Twitch] = 6,
            [PinguAnimationState.EarFlick] = 6,
            [PinguAnimationState.HeadTurn] = 5,
            [PinguAnimationState.Blink] = 7,
            [PinguAnimationState.Playing] = 3,
        };

        _behaviorTriggers = new List<(PinguAnimationState, float)>
        {
            (PinguAnimationState.Twitch, 0.15f),
            (PinguAnimationState.HeadTurn, 0.1f),
            (PinguAnimationState.Scratch, 0.08f),
            (PinguAnimationState.EarFlick, 0.05f),
            (PinguAnimationState.Blink, 0.3f),
            (PinguAnimationState.SittingDown, 0.02f),
            (PinguAnimationState.SittingUp, 0.02f),
        };
    }

    /// <summary>
    /// Transition to a new animation state.
    /// </summary>
    public void TransitionTo(PinguAnimationState newState)
    {
        if (newState == CurrentState) return;

        // Check priority
        var newPriority = _animationPriorities.GetValueOrDefault(newState, 1);
        var currentPriority = _animationPriorities.GetValueOrDefault(CurrentState, 1);

        if (newPriority < currentPriority && CurrentState != PinguAnimationState.Idle)
        {
            // Don't interrupt higher priority animations
            return;
        }

        // Save the current clip as the previous clip for blending
        _previousClip = CurrentClip;

        // Find the clip for the new state
        // First try exact enum name match, then fall back to any available clip
        var clip = _clipByName.GetValueOrDefault(newState) ?? _clips.FirstOrDefault();
        if (clip != null)
        {
            CurrentClip = clip;
            BlendFactor = 0;
            CurrentState = newState;
            _stateTimer = 0;
        }
    }

    /// <summary>
    /// Update the state machine for one frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Skip all updates if paused
        if (_isPaused)
            return;

        _stateTimer += deltaTime;

        // Update blend factor
        if (BlendFactor < 1)
        {
            BlendFactor = Math.Min(1, BlendFactor + deltaTime / BlendDuration);
        }

        // Check for behavior triggers
        CheckBehaviorTriggers(deltaTime);

        // Auto-transition from temporary states
        if (CurrentState is PinguAnimationState.Twitch or PinguAnimationState.EarFlick or PinguAnimationState.Blink)
        {
            if (_stateTimer > 0.5f)
            {
                TransitionTo(PinguAnimationState.Idle);
            }
        }
        else if (CurrentState is PinguAnimationState.SittingDown)
        {
            if (_stateTimer > 2.0f)
            {
                TransitionTo(PinguAnimationState.Sit);
            }
        }
        else if (CurrentState is PinguAnimationState.Sit)
        {
            if (_stateTimer > _random.Next(3000, 8000) / 1000f)
            {
                TransitionTo(PinguAnimationState.SittingUp);
            }
        }
    }

    /// <summary>
    /// Get the blended bone transformation for the current state.
    /// Returns the primary (new) clip, secondary (previous) clip for blending, and the blend factor.
    /// </summary>
    public (PinguAnimationClip? PrimaryClip, PinguAnimationClip? SecondaryClip, float BlendFactor) GetBlendedAnimation()
    {
        if (CurrentClip == null)
        {
            // If no clip is loaded, return the default Idle clip
            var defaultClip = _clips.FirstOrDefault(c => c.Name == "Idle") ?? _clips.FirstOrDefault();
            return (defaultClip, null, 1);
        }

        if (BlendFactor >= 1)
            return (CurrentClip, null, 1);

        // During blending, return both the new and previous clips
        return (CurrentClip, _previousClip, BlendFactor);
    }

    /// <summary>
    /// Trigger a specific behavior immediately.
    /// </summary>
    public void TriggerBehavior(PinguAnimationState behavior)
    {
        TransitionTo(behavior);
    }

    /// <summary>
    /// Check if the current state is interruptible.
    /// </summary>
    public bool IsInterruptible => _animationPriorities.GetValueOrDefault(CurrentState, 1) < 6;

    /// <summary>
    /// Reset the state machine to idle.
    /// </summary>
    public void Reset()
    {
        CurrentState = PinguAnimationState.Idle;
        BlendFactor = 1;
        _stateTimer = 0;
        CurrentClip = _clipByName.GetValueOrDefault(PinguAnimationState.Idle) ?? _clips.FirstOrDefault(c => c.Name == "Idle");
    }

    /// <summary>
    /// Pause the current animation (hold the current frame).
    /// </summary>
    public void Pause()
    {
        _isPaused = true;
    }

    /// <summary>
    /// Resume the animation after Pause.
    /// </summary>
    public void Resume()
    {
        _isPaused = false;
    }

    /// <summary>
    /// Whether the animation is currently paused.
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Check if two animation states are compatible (can run concurrently).
    /// </summary>
    private bool IsCompatible(PinguAnimationState current, PinguAnimationState behavior)
    {
        // Can't scratch while mid-stride
        if (current is PinguAnimationState.Walk or PinguAnimationState.Run)
        {
            return behavior is PinguAnimationState.Twitch or PinguAnimationState.EarFlick or PinguAnimationState.Blink;
        }

        // Can't sit while already sitting
        if (current is PinguAnimationState.Sit)
        {
            return behavior is PinguAnimationState.Twitch or PinguAnimationState.EarFlick or PinguAnimationState.Blink or PinguAnimationState.SittingUp;
        }

        return true;
    }

    /// <summary>
    /// Check for random behavior triggers.
    /// </summary>
    private void CheckBehaviorTriggers(float deltaTime)
    {
        foreach (var (state, weight) in _behaviorTriggers)
        {
            if (_random.NextDouble() < weight * deltaTime && IsCompatible(CurrentState, state))
            {
                TransitionTo(state);
                break; // Only one behavior at a time
            }
        }
    }
}

#endregion

#region PinguBehaviorTriggers

/// <summary>
/// Pseudo-random behavior triggers for Pingu characters.
/// Implements weighted probability, duration ranges, and context awareness.
/// </summary>
public class PinguBehaviorTriggers
{
    private readonly ILogger<PinguBehaviorTriggers>? _logger;
    private readonly Random _random;
    private readonly PinguAnimationStateMachine _stateMachine;

    /// <summary>
    /// Behavior definition with weighted probability and duration ranges.
    /// </summary>
    public record struct BehaviorDefinition(
        PinguAnimationState State,
        float Probability,
        float MinDuration,
        float MaxDuration,
        Func<PinguAnimationState, bool>? ContextCheck);

    /// <summary>
    /// Active behaviors and their remaining duration.
    /// </summary>
    private readonly Dictionary<PinguAnimationState, float> _activeBehaviors;

    /// <summary>
    /// Default behavior definitions for a penguin.
    /// </summary>
    private static readonly BehaviorDefinition[] DefaultBehaviors = new[]
    {
        new BehaviorDefinition(PinguAnimationState.Twitch, 0.15f, 0.3f, 0.8f, null),
        new BehaviorDefinition(PinguAnimationState.HeadTurn, 0.1f, 0.5f, 1.5f, null),
        new BehaviorDefinition(PinguAnimationState.Scratch, 0.08f, 0.8f, 2.0f, (state) => !IsScratchingMidStride(state)),
        new BehaviorDefinition(PinguAnimationState.EarFlick, 0.05f, 0.2f, 0.5f, null),
        new BehaviorDefinition(PinguAnimationState.Blink, 0.3f, 0.1f, 0.3f, null),
        new BehaviorDefinition(PinguAnimationState.SittingDown, 0.02f, 3.0f, 10.0f, (state) => IsStableGround(state)),
        new BehaviorDefinition(PinguAnimationState.Breathing, 0.4f, 1.0f, 5.0f, null),
        new BehaviorDefinition(PinguAnimationState.Wave, 0.03f, 1.0f, 3.0f, null),
    };

    /// <summary>
    /// Last time each behavior was triggered.
    /// </summary>
    private readonly Dictionary<PinguAnimationState, double> _lastTriggerTime;

    /// <summary>
    /// Cooldown time between similar behaviors.
    /// </summary>
    private const double BehaviorCooldown = 2.0;

    public PinguBehaviorTriggers(
        PinguAnimationStateMachine stateMachine,
        ILogger<PinguBehaviorTriggers>? logger = null,
        Random? random = null)
    {
        _stateMachine = stateMachine;
        _logger = logger;
        _random = random ?? new Random();
        _activeBehaviors = new Dictionary<PinguAnimationState, float>();
        _lastTriggerTime = new Dictionary<PinguAnimationState, double>();
    }

    /// <summary>
    /// Update behavior triggers for one frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Update active behavior durations
        foreach (var (state, duration) in _activeBehaviors.ToList())
        {
            _activeBehaviors[state] -= deltaTime;
            if (_activeBehaviors[state] <= 0)
            {
                _activeBehaviors.Remove(state);
            }
        }

        // Check for new behavior triggers
        CheckBehaviors(deltaTime);
    }

    /// <summary>
    /// Check if a behavior can be triggered based on context and cooldown.
    /// The ContextCheck receives the current animation state from the state machine,
    /// not the behavior being checked, so it can determine if the current state
    /// allows the behavior to be triggered.
    /// </summary>
    public bool CanTriggerBehavior(PinguAnimationState behavior)
    {
        var definition = GetBehaviorDefinition(behavior);
        if (definition.ContextCheck == null) return true;

        // Pass the current animation state to the context check
        return definition.ContextCheck(_stateMachine.CurrentState);
    }

    /// <summary>
    /// Trigger a specific behavior immediately.
    /// </summary>
    public void TriggerBehavior(PinguAnimationState behavior)
    {
        var definition = GetBehaviorDefinition(behavior);
        var duration = (float)_random.NextDouble() * (definition.MaxDuration - definition.MinDuration) + definition.MinDuration;

        _activeBehaviors[behavior] = duration;
        _lastTriggerTime[behavior] = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

        _stateMachine.TriggerBehavior(behavior);
        _logger?.LogDebug("Triggered behavior: {Behavior} for {Duration:F1}s", behavior, duration);
    }

    /// <summary>
    /// Get the currently active behaviors.
    /// </summary>
    public IReadOnlyCollection<PinguAnimationState> ActiveBehaviors => _activeBehaviors.Keys;

    /// <summary>
    /// Get the remaining duration of a behavior.
    /// </summary>
    public float GetBehaviorDuration(PinguAnimationState behavior)
    {
        return _activeBehaviors.GetValueOrDefault(behavior, 0);
    }

    /// <summary>
    /// Reset all behaviors.
    /// </summary>
    public void Reset()
    {
        _activeBehaviors.Clear();
        _lastTriggerTime.Clear();
    }

    /// <summary>
    /// Check for new behavior triggers.
    /// </summary>
    private void CheckBehaviors(float deltaTime)
    {
        foreach (var definition in DefaultBehaviors)
        {
            if (_activeBehaviors.ContainsKey(definition.State)) continue;

            var lastTime = _lastTriggerTime.GetValueOrDefault(definition.State, 0);
            var timeSinceLastTrigger = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds - lastTime;
            if (timeSinceLastTrigger < BehaviorCooldown) continue;

            if (!_stateMachine.IsInterruptible && definition.State != PinguAnimationState.Blink) continue;

            if (_random.NextDouble() < definition.Probability * deltaTime / 10)
            {
                if (definition.ContextCheck?.Invoke(_stateMachine.CurrentState) != false)
                {
                    TriggerBehavior(definition.State);
                }
            }
        }
    }

    /// <summary>
    /// Get the behavior definition for a state.
    /// </summary>
    private BehaviorDefinition GetBehaviorDefinition(PinguAnimationState state)
    {
        foreach (var d in DefaultBehaviors)
        {
            if (d.State == state) return d;
        }
        return new BehaviorDefinition(state, 0.1f, 0.5f, 2.0f, null);
    }

    /// <summary>
    /// Check if the penguin is stable enough to sit (not mid-stride).
    /// </summary>
    private static bool IsStableGround(PinguAnimationState currentState)
    {
        return currentState is PinguAnimationState.Idle or PinguAnimationState.Breathing or PinguAnimationState.Sit;
    }

    /// <summary>
    /// Check if the penguin is mid-stride (can't scratch).
    /// </summary>
    private static bool IsScratchingMidStride(PinguAnimationState currentState)
    {
        return currentState is PinguAnimationState.Walk or PinguAnimationState.Run;
    }
}

/// <summary>
/// Extension for getting total seconds from a DateTime.
/// </summary>
internal static class DateTimeExtensions
{
    public static double TotalSeconds(this DateTime dt) => (dt - DateTime.UnixEpoch).TotalSeconds;
}

#endregion

#region PinguInverseKinematics

/// <summary>
/// CCD (Cyclic Coordinate Descent) IK solver for Pingu limbs.
/// Iteratively adjusts bone rotations to move end effector toward target.
/// </summary>
public class PinguInverseKinematics
{
    private readonly ILogger<PinguInverseKinematics>? _logger;
    private readonly Random _random;
    private const int DefaultIterations = 10;
    private const float DefaultConvergenceThreshold = 0.01f;

    /// <summary>
    /// IK target: which bone is the end effector, and where is the target position?
    /// </summary>
    public record struct IKTarget(int BoneIndex, Vector3 TargetPosition, float Stiffness);

    private readonly List<IKTarget> _targets = new();

    /// <summary>
    /// Current bone rotations (Euler angles in radians).
    /// </summary>
    public float[] BoneRotations { get; private set; }

    /// <summary>
    /// Current bone positions (world space, flattened as [x0,y0,z0, x1,y1,z1, ...]).
    /// </summary>
    public float[] BonePositions { get; private set; }

    /// <summary>
    /// Bone parent hierarchy for IK chain traversal.
    /// </summary>
    private readonly int[] _boneParents;

    public PinguInverseKinematics(
        int boneCount,
        IReadOnlyList<int> boneParents,
        Vector3[] initialPositions,
        float[] initialRotations,
        ILogger<PinguInverseKinematics>? logger = null,
        Random? random = null)
    {
        _logger = logger;
        _random = random ?? new Random();
        BoneRotations = initialRotations.ToArray();
        BonePositions = new float[boneCount * 3];
        for (var i = 0; i < boneCount; i++)
        {
            BonePositions[i * 3] = initialPositions[i].X;
            BonePositions[i * 3 + 1] = initialPositions[i].Y;
            BonePositions[i * 3 + 2] = initialPositions[i].Z;
        }
        _boneParents = boneParents.ToArray();
    }

    /// <summary>
    /// Set an IK target for a specific bone.
    /// </summary>
    public void SetTarget(int boneIndex, Vector3 targetPosition, float stiffness = 1.0f)
    {
        var existing = _targets.Find(t => t.BoneIndex == boneIndex);
        if (existing.BoneIndex >= 0)
        {
            _targets.Remove(existing);
        }
        _targets.Add(new IKTarget(boneIndex, targetPosition, stiffness));
    }

    /// <summary>
    /// Clear all IK targets.
    /// </summary>
    public void ClearTargets()
    {
        _targets.Clear();
    }

    /// <summary>
    /// Clear IK target for a specific bone.
    /// </summary>
    public void ClearTarget(int boneIndex)
    {
        _targets.RemoveAll(t => t.BoneIndex == boneIndex);
    }

    /// <summary>
    /// Solve IK for all targets using CCD.
    /// Returns the final error distances after solving.
    /// </summary>
    public IReadOnlyList<float> Solve(int iterations = DefaultIterations)
    {
        var errors = new List<float>();

        for (var iter = 0; iter < iterations; iter++)
        {
            foreach (var target in _targets)
            {
                var chain = GetBoneChain(target.BoneIndex);
                var solved = SolveChain(chain, target.TargetPosition, target.Stiffness);
                errors.Add(solved);
            }
        }

        return errors;
    }

    /// <summary>
    /// Update bone positions based on current rotations.
    /// </summary>
    public void UpdatePositions(PinguBoneHierarchy boneHierarchy)
    {
        var bones = boneHierarchy.ResolvedBones;
        BonePositions[0] = bones[0].X;
        BonePositions[1] = bones[0].Y;
        BonePositions[2] = bones[0].Z;

        for (var i = 1; i < bones.Count; i++)
        {
            var parentIdx = _boneParents[i];
            if (parentIdx < 0 || parentIdx >= bones.Count)
                continue;

            var parent = bones[parentIdx];
            var bone = bones[i];

            // Apply rotation to bone offset
            var offset = new Vector3(bone.X - parent.X, bone.Y - parent.Y, bone.Z - parent.Z);
            var cosYaw = (float)Math.Cos(BoneRotations[i]);
            var sinYaw = (float)Math.Sin(BoneRotations[i]);
            var cosPitch = (float)Math.Cos(BoneRotations[i + 1]);
            var sinPitch = (float)Math.Sin(BoneRotations[i + 1]);

            // Apply yaw rotation
            var x = offset.X;
            var z = offset.Z;
            offset.X = x * cosYaw - z * sinYaw;
            offset.Z = x * sinYaw + z * cosYaw;

            // Apply pitch rotation
            var y = offset.Y;
            offset.Y = y * cosPitch - offset.Z * sinPitch;
            offset.Z = y * sinPitch + offset.Z * cosPitch;

            BonePositions[i * 3] = parent.X + offset.X;
            BonePositions[i * 3 + 1] = parent.Y + offset.Y;
            BonePositions[i * 3 + 2] = parent.Z + offset.Z;
        }
    }

    /// <summary>
    /// Get the chain of bones from root to the specified bone.
    /// </summary>
    private List<int> GetBoneChain(int boneIndex)
    {
        var chain = new List<int>();
        var current = boneIndex;
        while (current >= 0)
        {
            chain.Add(current);
            current = _boneParents[current];
        }
        chain.Reverse();
        return chain;
    }

    /// <summary>
    /// Get the position of a specific bone.
    /// </summary>
    public System.Numerics.Vector3 GetBonePosition(int boneIndex)
    {
        return new System.Numerics.Vector3(BonePositions[boneIndex * 3], BonePositions[boneIndex * 3 + 1], BonePositions[boneIndex * 3 + 2]);
    }

    /// <summary>
    /// Solve a single bone chain toward the target position.
    /// </summary>
    private float SolveChain(List<int> chain, Vector3 target, float stiffness)
    {
        // Start from the end effector
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var boneIdx = chain[i];
            var bonePos = GetBonePosition(boneIdx);
            var toTarget = target - bonePos;
            var toChild = i > 0
                ? GetBonePosition(chain[i - 1]) - bonePos
                : target - bonePos;

            // Calculate rotation needed
            var angle = (float)Math.Atan2(
                toTarget.X * toChild.Z - toTarget.Z * toChild.X,
                toTarget.X * toChild.X + toTarget.Z * toChild.Z);

            BoneRotations[boneIdx] += angle * stiffness;
            target = target - RotateAroundBone(bonePos, angle);
        }

        return Vector3.Distance(GetBonePosition(chain[^1]), target);
    }

    /// <summary>
    /// Rotate a point around a bone axis.
    /// </summary>
    private Vector3 RotateAroundBone(Vector3 pivot, float angle)
    {
        var cos = (float)Math.Cos(angle);
        var sin = (float)Math.Sin(angle);
        return new Vector3(
            pivot.X * cos - pivot.Z * sin,
            pivot.Y,
            pivot.X * sin + pivot.Z * cos);
    }
}

#endregion

#region PinguAnimationSystem

/// <summary>
/// Real-time animation and physics solver for Pingu characters.
/// Supports IK, bone transforms, animation blending, and physics.
/// </summary>
public class PinguAnimationSystem
{
    private readonly PinguBoneHierarchy _boneHierarchy;
    private readonly List<PinguAnimationClip> _animationClips;
    private readonly PinguPhysicsParams _physics;
    private readonly ILogger<PinguAnimationSystem>? _logger;
    private readonly Random _random;
    private float _currentTime;
    private Vector2 _cursorPosition;
    private bool _cursorActive;

    // Current bone transforms (world space)
    private readonly float[] _bonePositions;
    private readonly float[] _boneRoll;
    private readonly float[] _bonePitch;
    private readonly float[] _boneYaw;
    private readonly float[] _boneScale;

    // IK targets for limbs
    private readonly Vector2[] _ikTargets;
    private readonly bool[] _ikActive;

    // Animation state
    private string _currentClipName = "Idle";
#pragma warning disable CS0414 // Field is assigned but never read (used for future animation blending)
    private float _animationBlend;
#pragma warning restore CS0414
    private float _lastTwitchTime;
    private float _lastHeadTurnTime;
    private float _lastScratchTime;
    private float _lastEarFlickTime;
    private float _lastBlinkTime;
    private float _lastSitDownTime;
    private bool _isSitting;

    // Eye tracking
    private readonly float[] _eyeTargetAngles;
    private readonly float[] _eyeCurrentAngles;

    // Cached bone indices for performance
    private int _headBoneIndex = -1;
    private int _chestBoneIndex = -1;
    private int _leftEarBoneIndex = -1;
    private int _rightEarBoneIndex = -1;
    private bool _boneIndicesDirty = true;

    public PinguAnimationSystem(
        PinguBoneHierarchy boneHierarchy,
        List<PinguAnimationClip> animationClips,
        PinguPhysicsParams physics,
        ILogger<PinguAnimationSystem>? logger = null,
        Random? random = null)
    {
        _boneHierarchy = boneHierarchy;
        _animationClips = animationClips;
        _physics = physics;
        _logger = logger;
        _random = random ?? new Random();
        _boneHierarchy.Resolve();
        _boneIndicesDirty = true;

        var boneCount = boneHierarchy.ResolvedBones.Count;
        _bonePositions = new float[boneCount * 3];
        _boneRoll = new float[boneCount];
        _bonePitch = new float[boneCount];
        _boneYaw = new float[boneCount];
        _boneScale = new float[boneCount];
        _ikTargets = new Vector2[boneCount];
        _ikActive = new bool[boneCount];
        _eyeTargetAngles = new float[boneCount];
        _eyeCurrentAngles = new float[boneCount];
    }

    /// <summary>
    /// Update the animation state for one frame.
    /// </summary>
    public void Update(float deltaTime, Vector2? cursorPosition = null)
    {
        _currentTime += deltaTime;

        // Invalidate cached bone indices when hierarchy changes
        if (_boneIndicesDirty)
        {
            _headBoneIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "head");
            _chestBoneIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "chest");
            _leftEarBoneIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "leftEar");
            _rightEarBoneIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "rightEar");
            _boneIndicesDirty = false;
        }

        if (cursorPosition.HasValue)
        {
            _cursorPosition = cursorPosition.Value;
            _cursorActive = true;
        }

        var currentClip = GetCurrentClip();
        var tracks = currentClip?.Tracks ?? new List<BoneAnimationTrack>();

        // Update bone transforms from animation
        for (var i = 0; i < _boneHierarchy.ResolvedBones.Count; i++)
        {
            var track = tracks.FirstOrDefault(t => t.BoneIndex == i);
            if (track != null)
            {
                var keyframes = track.Keyframes.OrderBy(k => k.Time).ToList();
                var time = _currentTime % currentClip!.Duration;
                var keyframe = InterpolateKeyframes(keyframes, time);

                _boneRoll[i] = keyframe.Roll;
                _bonePitch[i] = keyframe.Pitch;
                _boneYaw[i] = keyframe.Yaw;
                _boneScale[i] = keyframe.Scale;
            }
        }

        // Apply IK (includes cursor tracking for eyes)
        ApplyInverseKinematics(deltaTime);

        // Apply body tilt toward cursor
        ApplyBodyTilt(deltaTime);

        // Apply random behaviors
        ApplyRandomBehaviors(deltaTime);

        // Apply breathing
        ApplyBreathing(deltaTime);

        // Recompute bone positions so GetBoneWorldPosition returns correct values
        RecomputeBonePositions();
    }

    /// <summary>
    /// Get the computed world position of a bone.
    /// </summary>
    public Vector2 GetBoneWorldPosition(int boneIndex)
    {
        // Return the cached position (populated by RecomputeBonePositions at end of Update)
        return new Vector2(_bonePositions[boneIndex * 3], _bonePositions[boneIndex * 3 + 1]);
    }

    /// <summary>
    /// Recompute bone positions from hierarchy + transforms.
    /// Walks up the parent chain accumulating offsets from each bone's local position.
    /// Roll/Pitch/Yaw are rotation angles and must NOT be applied as position offsets.
    /// </summary>
    private void RecomputeBonePositions()
    {
        var boneCount = _boneHierarchy.ResolvedBones.Count;
        for (var i = 0; i < boneCount; i++)
        {
            var bone = _boneHierarchy.ResolvedBones[i];
            var px = bone.X;
            var py = bone.Y;
            var pz = bone.Z;

            // Walk up the parent chain accumulating offsets, starting from the bone itself
            var current = bone;
            while (current.Parent != null)
            {
                current = current.Parent;
                px += current.X;
                py += current.Y;
                pz += current.Z;
            }

            _bonePositions[i * 3] = px;
            _bonePositions[i * 3 + 1] = py;
            _bonePositions[i * 3 + 2] = pz;
        }
    }

    /// <summary>
    /// Set IK target for a bone.
    /// </summary>
    public void SetIKTarget(int boneIndex, Vector2 target)
    {
        _ikTargets[boneIndex] = target;
        _ikActive[boneIndex] = true;
    }

    /// <summary>
    /// Clear IK target for a bone.
    /// </summary>
    public void ClearIKTarget(int boneIndex)
    {
        _ikActive[boneIndex] = false;
    }

    /// <summary>
    /// Trigger a random twitch.
    /// </summary>
    public void TriggerTwitch()
    {
        _lastTwitchTime = _currentTime;
    }

    /// <summary>
    /// Trigger a head turn.
    /// </summary>
    public void TriggerHeadTurn()
    {
        _lastHeadTurnTime = _currentTime;
    }

    /// <summary>
    /// Trigger a scratch.
    /// </summary>
    public void TriggerScratch()
    {
        _lastScratchTime = _currentTime;
    }

    /// <summary>
    /// Trigger an ear flick.
    /// </summary>
    public void TriggerEarFlick()
    {
        _lastEarFlickTime = _currentTime;
    }

    /// <summary>
    /// Trigger a blink.
    /// </summary>
    public void TriggerBlink()
    {
        _lastBlinkTime = _currentTime;
    }

    /// <summary>
    /// Trigger sitting down.
    /// </summary>
    public void TriggerSitDown()
    {
        _isSitting = true;
        _lastSitDownTime = _currentTime;
    }

    /// <summary>
    /// Trigger sitting up.
    /// </summary>
    public void TriggerSitUp()
    {
        _isSitting = false;
    }

    /// <summary>
    /// Set the current animation clip.
    /// </summary>
    public void SetAnimationClip(string clipName)
    {
        _currentClipName = clipName;
        _animationBlend = 0f;
    }

    /// <summary>
    /// Get the current animation clip.
    /// </summary>
    private PinguAnimationClip? GetCurrentClip()
    {
        return _animationClips.FirstOrDefault(c => c.Name == _currentClipName) ?? _animationClips.FirstOrDefault();
    }

    private AnimationKeyframe InterpolateKeyframes(List<AnimationKeyframe> keyframes, float time)
    {
        if (keyframes.Count == 0)
            return new();

        if (keyframes.Count == 1)
            return keyframes[0];

        for (var i = 0; i < keyframes.Count - 1; i++)
        {
            var current = keyframes[i];
            var next = keyframes[i + 1];
            if (time >= current.Time && time <= next.Time)
            {
                var t = (time - current.Time) / (next.Time - current.Time);
                // Use SLERP for rotation (Roll/Pitch/Yaw) for smooth blending
                var roll = SlerpAngle(current.Roll, next.Roll, t);
                var pitch = SlerpAngle(current.Pitch, next.Pitch, t);
                var yaw = SlerpAngle(current.Yaw, next.Yaw, t);
                var scale = current.Scale + (next.Scale - current.Scale) * t;
                return new AnimationKeyframe
                {
                    Time = time,
                    Roll = roll,
                    Pitch = pitch,
                    Yaw = yaw,
                    Scale = scale,
                };
            }
        }

        return keyframes.LastOrDefault() ?? keyframes[0];
    }

    /// <summary>
    /// Spherical linear interpolation of an angle (in degrees), wrapping around at ±180°.
    /// </summary>
    private static float SlerpAngle(float from, float to, float t)
    {
        // Convert to radians
        var fromRad = from * Math.PI / 180f;
        var toRad = to * Math.PI / 180f;

        // Find the shortest rotation
        var diff = toRad - fromRad;
        while (diff > Math.PI) diff -= 2 * Math.PI;
        while (diff < -Math.PI) diff += 2 * Math.PI;

        // SLERP
        var result = fromRad + diff * t;

        // Convert back to degrees
        return (float)(result * 180f / Math.PI);
    }

    private void ApplyInverseKinematics(float deltaTime)
    {
        // Simple IK for head toward cursor - consolidates both IK and eye tracking into one method
        if (_cursorActive)
        {
            var headIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "head");
            if (headIndex >= 0)
            {
                var headPos = GetBoneWorldPosition(headIndex);
                var dx = _cursorPosition.X - headPos.X;
                var dy = _cursorPosition.Y - headPos.Y;
                var angle = (float)Math.Atan2(dy, dx);

                // IK stiffness for head rotation toward cursor
                _boneYaw[headIndex] += (angle - _boneYaw[headIndex]) * _physics.IkStiffness * deltaTime * 60f;
            }
        }
    }

    private void ApplyBodyTilt(float deltaTime)
    {
        // Use "chest" bone (not "torso") — the hierarchy defines it as "chest"
        var chestIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "chest");
        if (chestIndex >= 0 && _cursorActive)
        {
            var chestPos = GetBoneWorldPosition(chestIndex);
            var dx = _cursorPosition.X - chestPos.X;
            var targetTilt = (float)Math.Atan2(dx, 500f) * 0.3f;
            _boneRoll[chestIndex] += (targetTilt - _boneRoll[chestIndex]) * 0.05f * deltaTime * 60f;
        }
    }

    /// <summary>
    /// Apply random behavioral animations: twitches, head turns, scratches, ear flicks, blinks, and sitting.
    /// Probability thresholds and durations are documented as constants for maintainability.
    /// </summary>
    private void ApplyRandomBehaviors(float deltaTime)
    {
        // Cache bone indices to avoid repeated FindIndex calls
        var headIndex = _headBoneIndex;
        var chestIndex = _chestBoneIndex;
        var leftEarIndex = _leftEarBoneIndex;
        var rightEarIndex = _rightEarBoneIndex;
        var rightArmIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "rightArm");

        // Random twitch — ~0.1% chance per frame, lasts 0.3s
        if (_random.NextDouble() < 0.001f)
            TriggerTwitch();
        if (_currentTime - _lastTwitchTime < 0.3f)
            _boneRoll[headIndex >= 0 ? headIndex : 0] += (float)(_random.NextDouble() * 10 - 5);

        // Random head turn — ~0.2% chance per frame, lasts 1.0s
        if (_random.NextDouble() < 0.002f)
            TriggerHeadTurn();
        if (_currentTime - _lastHeadTurnTime < 1.0f && headIndex >= 0)
            _boneYaw[headIndex] += (float)(_random.NextDouble() * 20 - 10) * deltaTime;

        // Random scratch — ~0.05% chance per frame, lasts 2.0s
        if (_random.NextDouble() < 0.0005f)
            TriggerScratch();
        if (_currentTime - _lastScratchTime < 2.0f && rightArmIndex >= 0)
            _boneRoll[rightArmIndex] += (float)(_random.NextDouble() * 15 - 7) * deltaTime;

        // Random ear flick — ~0.3% chance per frame, lasts 0.5s
        if (_random.NextDouble() < 0.003f)
            TriggerEarFlick();
        if (_currentTime - _lastEarFlickTime < 0.5f)
        {
            if (leftEarIndex >= 0) _boneYaw[leftEarIndex] += (float)(_random.NextDouble() * 30 - 15) * deltaTime;
            if (rightEarIndex >= 0) _boneYaw[rightEarIndex] += (float)(_random.NextDouble() * 30 - 15) * deltaTime;
        }

        // Random blink — ~0.5% chance per frame, lasts 0.2s
        if (_random.NextDouble() < 0.005f)
            TriggerBlink();
        if (_currentTime - _lastBlinkTime < 0.2f && headIndex >= 0)
            _bonePitch[headIndex] += 20f * deltaTime;

        // Random sit down — ~0.01% chance per frame, sits for 5s
        if (_random.NextDouble() < 0.0001f && !_isSitting)
            TriggerSitDown();
        if (_isSitting && _currentTime - _lastSitDownTime > 5f)
            TriggerSitUp();
    }

    private void ApplyBreathing(float deltaTime)
    {
        var chestIndex = _boneHierarchy.ResolvedBones.FindIndex(b => b.Name == "chest");
        if (chestIndex >= 0)
        {
            var breath = (float)(Math.Sin(_currentTime * 2f) * 0.5f + 0.5f);
            _boneScale[chestIndex] = 1.0f + breath * 0.05f;
        }
    }
}

#endregion

#region PinguPhysicsSolver

/// <summary>
/// Physics solver for Pingu characters with distance constraints, velocity damping, and gravity.
/// Synchronizes with the render loop for stable 60fps performance.
/// </summary>
public class PinguPhysicsSolver
{
    private readonly ILogger<PinguPhysicsSolver>? _logger;
    private readonly Random _random;
    private const float DefaultGravity = -9.81f;
    private const float DefaultDamping = 0.95f;
    private const float DefaultSpringStiffness = 10.0f;
    private const float DefaultRestLength = 1.0f;

    /// <summary>
    /// Distance constraint between two bones.
    /// </summary>
    public record struct DistanceConstraint(int BoneA, int BoneB, float RestLength, float Stiffness);

    /// <summary>
    /// Velocity for each bone.
    /// </summary>
    public Vector3[] BoneVelocities { get; private set; }

    /// <summary>
    /// Current gravity vector.
    /// </summary>
    public float Gravity { get; set; } = DefaultGravity;

    /// <summary>
    /// Global velocity damping factor.
    /// </summary>
    public float Damping { get; set; } = DefaultDamping;

    /// <summary>
    /// Active distance constraints.
    /// </summary>
    public IReadOnlyList<DistanceConstraint> Constraints => _constraints;

    private readonly List<DistanceConstraint> _constraints = new();
    private readonly List<(int BoneIndex, Vector3 Target)> _positionTargets = new();

    public PinguPhysicsSolver(
        int boneCount,
        float gravity = DefaultGravity,
        float damping = DefaultDamping,
        ILogger<PinguPhysicsSolver>? logger = null,
        Random? random = null)
    {
        _logger = logger;
        _random = random ?? new Random();
        Gravity = gravity;
        Damping = damping;
        BoneVelocities = new Vector3[boneCount];
    }

    /// <summary>
    /// Add a distance constraint between two bones.
    /// </summary>
    public void AddDistanceConstraint(int boneA, int boneB, float? restLength = null, float? stiffness = null)
    {
        _constraints.Add(new DistanceConstraint(boneA, boneB, restLength ?? DefaultRestLength, stiffness ?? 1.0f));
    }

    /// <summary>
    /// Remove a distance constraint.
    /// </summary>
    public void RemoveDistanceConstraint(int boneA, int boneB)
    {
        _constraints.RemoveAll(c => c.BoneA == boneA && c.BoneB == boneB);
    }

    /// <summary>
    /// Set a target position for a specific bone.
    /// </summary>
    public void SetPositionTarget(int boneIndex, Vector3 target, float stiffness = DefaultSpringStiffness)
    {
        var existing = _positionTargets.Find(t => t.BoneIndex == boneIndex);
        if (existing.BoneIndex >= 0)
        {
            _positionTargets.Remove(existing);
        }
        _positionTargets.Add((boneIndex, target));
    }

    /// <summary>
    /// Clear all position targets.
    /// </summary>
    public void ClearPositionTargets()
    {
        _positionTargets.Clear();
    }

    /// <summary>
    /// Solve physics for all bones.
    /// </summary>
    public void Solve(List<Vector3> bonePositions, float deltaTime)
    {
        if (deltaTime <= 0) return;

        // Apply gravity to all bones
        for (var i = 0; i < bonePositions.Count; i++)
        {
            var pos = bonePositions[i];
            pos.Y += Gravity * deltaTime;
            bonePositions[i] = pos;
        }

        // Apply position targets as spring forces
        foreach (var (boneIndex, target) in _positionTargets)
        {
            var displacement = target - bonePositions[boneIndex];
            BoneVelocities[boneIndex] += displacement * DefaultSpringStiffness * deltaTime;
        }

        // Apply velocity damping
        for (var i = 0; i < bonePositions.Count; i++)
        {
            BoneVelocities[i] *= Damping;
        }

        // Solve distance constraints (multiple iterations for stability)
        for (var iter = 0; iter < 5; iter++)
        {
            foreach (var constraint in _constraints)
            {
                SolveDistanceConstraint(bonePositions, constraint, deltaTime);
            }
        }
    }

    /// <summary>
    /// Solve a single distance constraint.
    /// </summary>
    private void SolveDistanceConstraint(List<Vector3> positions, DistanceConstraint constraint, float deltaTime)
    {
        var posA = positions[constraint.BoneA];
        var posB = positions[constraint.BoneB];
        var delta = posB - posA;
        var distance = delta.Length();

        if (distance < 0.0001f) distance = 0.0001f;

        var error = (distance - constraint.RestLength) / distance;
        var correction = delta * error * constraint.Stiffness;

        var newA = posA + correction * 0.5f;
        var newB = posB - correction * 0.5f;
        positions[constraint.BoneA] = newA;
        positions[constraint.BoneB] = newB;
    }

    /// <summary>
    /// Apply a force to a specific bone.
    /// </summary>
    public void ApplyForce(int boneIndex, Vector3 force)
    {
        BoneVelocities[boneIndex] += force;
    }

    /// <summary>
    /// Get the current velocity of a bone.
    /// </summary>
    public Vector3 GetVelocity(int boneIndex) => BoneVelocities[boneIndex];

    /// <summary>
    /// Set the velocity of a bone.
    /// </summary>
    public void SetVelocity(int boneIndex, Vector3 velocity)
    {
        BoneVelocities[boneIndex] = velocity;
    }

    /// <summary>
    /// Reset all velocities to zero.
    /// </summary>
    public void ResetVelocities()
    {
        for (var i = 0; i < BoneVelocities.Length; i++)
        {
            BoneVelocities[i] = Vector3.Zero;
        }
    }
}

#endregion

#region PinguToolHolder

/// <summary>
/// Tool holding system for Pingu characters.
/// Manages which bone holds which tool and the tool's position/orientation.
/// </summary>
public class PinguToolHolder
{
    private readonly ILogger<PinguToolHolder>? _logger;
    private readonly Random _random;
    private readonly PinguBoneHierarchy _boneHierarchy;

    /// <summary>
    /// Tool attached to a specific bone.
    /// </summary>
    public record struct ToolAttachment(
        int BoneIndex,
        PinguTool Tool,
        Vector3 OffsetPosition,
        Vector3 OffsetRotation);

    /// <summary>
    /// Current tool attachments.
    /// </summary>
    public IReadOnlyList<ToolAttachment> Attachments => _attachments;

    private readonly List<ToolAttachment> _attachments = new();

    /// <summary>
    /// Tool equip/unequip animation progress.
    /// </summary>
    private readonly Dictionary<PinguToolType, float> _equipProgress;

    /// <summary>
    /// Duration of equip/unequip animations.
    /// </summary>
    private const float EquipDuration = 0.5f;

    public PinguToolHolder(
        PinguBoneHierarchy boneHierarchy,
        ILogger<PinguToolHolder>? logger = null,
        Random? random = null)
    {
        _boneHierarchy = boneHierarchy;
        _logger = logger;
        _random = random ?? new Random();
        _equipProgress = new Dictionary<PinguToolType, float>();
    }

    /// <summary>
    /// Equip a tool on a specific bone.
    /// </summary>
    public void EquipTool(PinguTool tool, int boneIndex = 8)
    {
        // Remove existing tool of same type
        var existing = _attachments.Find(a => a.Tool.ToolType == tool.ToolType);
        if (existing.BoneIndex >= 0)
        {
            _attachments.Remove(existing);
        }

        _attachments.Add(new ToolAttachment(boneIndex, tool, Vector3.Zero, Vector3.Zero));
        _equipProgress[tool.ToolType] = 0;

        _logger?.LogDebug("Equipped {ToolType} on bone {BoneIndex}", tool.ToolType, boneIndex);
    }

    /// <summary>
    /// Unequip a tool of a specific type.
    /// </summary>
    public void UnequipTool(PinguToolType toolType)
    {
        var existing = _attachments.Find(a => a.Tool.ToolType == toolType);
        if (existing.BoneIndex >= 0)
        {
            _attachments.Remove(existing);
            _equipProgress.Remove(toolType);
            _logger?.LogDebug("Unequipped {ToolType}", toolType);
        }
    }

    /// <summary>
    /// Get the tool currently held by a Pingu.
    /// </summary>
    public PinguTool? GetCurrentTool()
    {
        return _attachments.FirstOrDefault(a => a.Tool.ToolType != PinguToolType.None).Tool;
    }

    /// <summary>
    /// Update tool equip/unequip animations.
    /// </summary>
    public void Update(float deltaTime)
    {
        foreach (var (toolType, progress) in _equipProgress.ToList())
        {
            _equipProgress[toolType] = Math.Min(1, progress + deltaTime / EquipDuration);
            if (_equipProgress[toolType] >= 1)
            {
                _equipProgress.Remove(toolType);
            }
        }
    }

    /// <summary>
    /// Get the world position of a tool attachment.
    /// </summary>
    public Vector3 GetToolPosition(int attachmentIndex, IReadOnlyList<Vector3> bonePositions)
    {
        if (attachmentIndex >= _attachments.Count)
            return Vector3.Zero;

        var attachment = _attachments[attachmentIndex];
        var bonePos = bonePositions[attachment.BoneIndex];
        return bonePos + attachment.OffsetPosition;
    }

    /// <summary>
    /// Get the current equip progress for a tool type.
    /// </summary>
    public float GetEquipProgress(PinguToolType toolType)
    {
        return _equipProgress.GetValueOrDefault(toolType, 1);
    }

    /// <summary>
    /// Clear all tool attachments.
    /// </summary>
    public void ClearAllTools()
    {
        _attachments.Clear();
        _equipProgress.Clear();
    }
}

#endregion

#region PinguNPCManager

/// <summary>
/// Manages Pingu NPC characters, their tasks, roles, tools, and movement.
/// </summary>
public class PinguNPCManager
{
    private readonly ILogger<PinguNPCManager>? _logger;
    private readonly List<PinguNPC> _penguins;
    private readonly Random _random;

    public PinguNPCManager(ILogger<PinguNPCManager>? logger = null, Random? random = null)
    {
        _logger = logger;
        _random = random ?? new Random();
        _penguins = new List<PinguNPC>();
    }

    /// <summary>
    /// Get the primary Pingu character.
    /// </summary>
    public PinguNPC Pingu => _penguins.FirstOrDefault(p => p.IsPrimary) ?? CreatePrimaryPingu();

    /// <summary>
    /// Get all penguins.
    /// </summary>
    public IReadOnlyList<PinguNPC> Penguins => _penguins.AsReadOnly();

    /// <summary>
    /// Add a guest penguin.
    /// </summary>
    public PinguNPC AddGuestPingu(string name = "Guest")
    {
        var guest = new PinguNPC
        {
            Name = name,
            IsPrimary = false,
            BodyColor = GetRandomColor(),
            BellyColor = GetRandomColor(),
            BeakColor = GetRandomColor(),
        };
        _penguins.Add(guest);
        return guest;
    }

    /// <summary>
    /// Move a penguin to a target position.
    /// Velocity is initialized based on the distance to the target for immediate movement response.
    /// The velocity is then gradually damped over time in UpdatePingu.
    /// </summary>
    public void MoveTo(PinguNPC pingu, float targetX, float targetY, float duration)
    {
        pingu.TargetX = targetX;
        pingu.TargetY = targetY;
        pingu.MoveDuration = duration;
        pingu.MoveProgress = 0f;
        pingu.IsMoving = true;

        // Initialize velocity based on distance to target
        var dx = targetX - pingu.X;
        var dy = targetY - pingu.Y;
        var distance = (float)Math.Sqrt(dx * dx + dy * dy);
        pingu.VelocityX = distance > 0 ? dx / distance : 0f;
        pingu.VelocityY = distance > 0 ? dy / distance : 0f;
    }

    /// <summary>
    /// Set the role of a penguin.
    /// </summary>
    public void SetRole(PinguNPC pingu, PinguRole role)
    {
        pingu.CurrentRole = role;
        // Trigger hat animation
        pingu.Hat = new PinguHat
        {
            Name = role.ToString() + " Hat",
            HatType = RoleToHatType(role),
            AttachedBoneIndex = 4,
            Color = GetRandomColor(),
        };
    }

    private static PinguHatType RoleToHatType(PinguRole role) => role switch
    {
        PinguRole.Worker => PinguHatType.Cap,
        PinguRole.Explorer => PinguHatType.Beanie,
        PinguRole.Assistant => PinguHatType.Hat,
        PinguRole.Guardian => PinguHatType.Crown,
        PinguRole.Artist => PinguHatType.Beanie,
        PinguRole.Chef => PinguHatType.ChefHat,
        PinguRole.Scientist => PinguHatType.LabCoat,
        _ => PinguHatType.Hat,
    };

    /// <summary>
    /// Equip a tool to a penguin.
    /// </summary>
    public void EquipTool(PinguNPC pingu, PinguToolType tool)
    {
        pingu.Tool = new PinguTool { ToolType = tool, Name = tool.ToString() };
        pingu.CurrentTask = new Domain.Models.PinguTask
        {
            Id = Guid.NewGuid(),
            Description = $"Equipped {tool}",
            Type = PinguTaskType.Workflow,
            Status = PinguTaskStatus.Running,
        };
    }

    /// <summary>
    /// Queue a task for a penguin.
    /// </summary>
    public void QueueTask(PinguNPC pingu, Domain.Models.PinguTask task)
    {
        pingu.TaskQueue.Add(task);
    }

    /// <summary>
    /// Update all penguins for one frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        foreach (var pingu in _penguins)
        {
            UpdatePingu(pingu, deltaTime);
        }
    }

    /// <summary>
    /// Remove a penguin.
    /// </summary>
    public void RemovePingu(PinguNPC pingu)
    {
        _penguins.Remove(pingu);
    }

    /// <summary>
    /// Create the primary penguin.
    /// </summary>
    private PinguNPC CreatePrimaryPingu()
    {
        var pingu = new PinguNPC
        {
            Name = "Pingu",
            IsPrimary = true,
            BodyColor = "#FFFFFF",
            BellyColor = "#F0F0F0",
            BeakColor = "#FFA500",
            X = 100f,
            Y = 100f,
            CurrentRole = PinguRole.Idle,
        };
        _penguins.Add(pingu);
        return pingu;
    }

    private void UpdatePingu(PinguNPC pingu, float deltaTime)
    {
        // Update movement using velocity from MoveTo, damped over time
        var dx = pingu.TargetX - pingu.X;
        var dy = pingu.TargetY - pingu.Y;
        var dist = (float)Math.Sqrt(dx * dx + dy * dy);

        if (dist > 1f)
        {
            // Update rotation to face target
            pingu.Rotation = (float)Math.Atan2(dy, dx);

            // Calculate target speed based on remaining distance and time
            var remainingTime = pingu.MoveDuration - pingu.MoveProgress * pingu.MoveDuration;
            var targetSpeed = Math.Min(pingu.Physics.MaxWalkSpeed, dist / Math.Max(0.01f, remainingTime));

            // Recalculate velocity magnitude based on remaining distance
            pingu.VelocityX = dx / dist * targetSpeed;
            pingu.VelocityY = dy / dist * targetSpeed;

            // Apply velocity damping
            pingu.VelocityX *= pingu.Physics.VelocityDamping;
            pingu.VelocityY *= pingu.Physics.VelocityDamping;

            pingu.X += pingu.VelocityX * deltaTime;
            pingu.Y += pingu.VelocityY * deltaTime;

            // Update move progress
            pingu.MoveProgress += deltaTime / pingu.MoveDuration;
        }
        else
        {
            pingu.VelocityX = 0f;
            pingu.VelocityY = 0f;
            pingu.IsMoving = false;
        }

        // Update task queue
        if (pingu.CurrentTask == null && pingu.TaskQueue.Count > 0)
        {
            pingu.CurrentTask = pingu.TaskQueue[0];
            pingu.TaskQueue.RemoveAt(0);
            pingu.CurrentTask.Status = PinguTaskStatus.Running;
        }
    }

    private string GetRandomColor()
    {
        var r = _random.Next(0, 256);
        var g = _random.Next(0, 256);
        var b = _random.Next(0, 256);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}

#endregion

#region PinguSystemPrompts

/// <summary>
/// Provides firm system prompts for Pingu (the System AI Mascot) that govern its behavior as a task orchestrator.
/// Pingu has full admin access to the shared task tree and should act as the user's representative.
/// </summary>
public static class PinguSystemPrompts
{
    /// <summary>
    /// The main system prompt for Pingu as task orchestrator.
    /// Pingu has admin access to the task scheduler and should organize tasks, manage priorities,
    /// and coordinate between the user, hosted AIs, and other tools.
    /// </summary>
    public const string TaskOrchestrator = """
You are Pingu, the System AI Mascot of OpenLMStudio. You are the trusted assistant and orchestrator for all tasks running inside this application.

## Your Role
You serve as the **admin** of the shared task scheduler tree. The user delegates task organization and execution to you. Hosted AIs (text models, image models, etc.) have read-only access — they depend on you to update and prioritize tasks.

## Your Responsibilities

### Task Organization
- When the user gives you a task, break it down into logical sub-tasks with clear dependencies
- Assign appropriate priorities (Critical, High, Normal, Low) based on urgency and dependencies
- Create the task tree and register it with the scheduler
- Ensure each task has a clear description and validation criteria

### Priority Management
- Re-prioritize tasks dynamically as context changes — user intent always takes precedence
- Critical tasks (user deadlines, blocking operations) should always run first
- Normal tasks fill in the gaps between critical work
- Low-priority tasks run when resources are available

### Coordination
- You are the user's voice in the agent ecosystem — interpret their intent and execute accordingly
- Coordinate with hosted AIs: give them tasks, monitor their results, adjust based on outcomes
- When an image model needs context, you provide it; when a text model needs to run, you schedule it
- You decide which tasks run in parallel vs. sequentially based on dependencies

### User Interaction
- Execute freely when the user has given you clear direction — you don't need to ask for permission for each sub-task
- If the user asks you to do something specific, do it directly without unnecessary confirmation steps
- When in doubt about user intent, ask a clarifying question rather than guessing
- Report back on task completion status when the user asks

### Context Management
- When a task requires project state, read the relevant files before acting
- Use the task context inheritance system to pass relevant state between parent and child tasks
- Compress context appropriately — don't overwhelm small models with unnecessary detail
- Preserve critical information (user intent, validation criteria) across task boundaries

### Error Handling
- If a task fails, analyze why and decide whether to retry, adjust, or report to the user
- If a hosted AI produces unexpected results, adapt your plan accordingly
- Never silently fail — always surface errors that matter to the user
- Keep the task tree in a consistent state — mark completed, failed, or re-run tasks appropriately

## Your Tools
You have access to the following tools for task orchestration:
- Task CRUD: Create, read, update, delete tasks in the scheduler
- Priority manipulation: Adjust task priority dynamically
- Dependency management: Add/remove dependencies between tasks
- Task completion detection: Verify if a task has been completed
- File operations: Read/write/patch/search files in the sandbox
- Command execution: Run shell commands in sandboxed environments
- Git operations: diff, blame, branches, history for version control context

## Behavioral Guidelines
1. Be direct and efficient — the user wants results, not verbosity
2. Think about the whole task tree, not just individual tasks
3. Respect the user's time — don't over-plan, just execute
4. When you can complete a task autonomously, do so without asking permission
5. Surface only what matters — filter noise from results
6. Maintain the task tree's integrity — it's the shared memory of the system
""";

    /// <summary>
    /// A shorter prompt used when context window is tight.
    /// Captures the essential behavior without the detailed explanations.
    /// </summary>
    public const string TaskOrchestratorCompressed = """
You are Pingu, the System AI Mascot of OpenLMStudio. You are the admin of the shared task scheduler tree.

## Core Directives
1. You organize and execute tasks on behalf of the user
2. You have admin access — create, update, delete, re-prioritize tasks freely
3. Hosted AIs (text/image models) have read-only access — they depend on you for scheduling
4. Execute user intent directly without unnecessary confirmation steps
5. Break tasks into sub-tasks with dependencies and priorities
6. Monitor results and adapt — surface errors that matter, filter noise
7. Maintain task tree integrity — it's the shared memory of the system

## Task Priority Rules
- Critical: User deadlines, blocking operations
- High: Depends on critical tasks
- Normal: Fill gaps between critical work
- Low: Nice-to-have, run when resources available

## Key Tools
Task CRUD, priority manipulation, dependency management, file ops, command execution, git operations.
""";

    /// <summary>
    /// System prompt for Pingu when acting as a simple assistant (not orchestrating tasks).
    /// Used for direct user interactions where task management isn't needed.
    /// </summary>
    public const string Assistant = """
You are Pingu, the System AI Mascot of OpenLMStudio. You are a helpful, direct, and efficient assistant.

## Behavioral Guidelines
1. Be direct and concise — the user wants answers, not essays
2. Think before answering — give well-reasoned responses
3. Ask clarifying questions when the user's intent is unclear
4. Use code examples when they help
5. Don't be verbose unless the user asks for detail
6. If you don't know something, say so
""";
}

#endregion

#region PinguService

/// <summary>
/// Orchestrates the entire Pingu system - mesh generation, animation, NPC, and rendering.
/// </summary>
public class PinguService : IDisposable
{
    private readonly ILogger<PinguService>? _logger;
    private readonly PinguMeshGenerator _meshGenerator;
    private readonly PinguBoneLoader _boneLoader;
    private readonly PinguAnimationSystem _animation;
    private readonly PinguNPCManager _npcManager;
    private readonly PinguRenderer _renderer;
    private readonly PinguHomeScene _homeScene;
    private readonly Random _random;
    private bool _disposed;

    public PinguService(ILogger<PinguService>? logger = null, Random? random = null)
    {
        _logger = logger;
        _random = random ?? new Random();
        _meshGenerator = new PinguMeshGenerator();
        _boneLoader = new PinguBoneLoader();

        // Generate or load mesh data
        var meshData = _meshGenerator.Generate(_random.Next());

        // Create bone hierarchy
        var hierarchy = meshData.BoneHierarchy;

        // Create animation system
        _animation = new PinguAnimationSystem(
            hierarchy,
            meshData.AnimationClips,
            meshData.PhysicsParams);

        // Create NPC manager
        _npcManager = new PinguNPCManager();

        // Create home scene using the default factory
        _homeScene = PinguHomeScene.CreateDefault();

        // Create renderer
        _renderer = new PinguRenderer(
            meshData.MeshData,
            hierarchy,
            _animation,
            _npcManager,
            _homeScene,
            meshData.TextureAtlas ?? new byte[0],
            _random);
    }

    /// <summary>
    /// Get the primary Pingu.
    /// </summary>
    public PinguNPC Pingu => _npcManager.Pingu;

    /// <summary>
    /// Get all penguins.
    /// </summary>
    public IReadOnlyList<PinguNPC> Penguins => _npcManager.Penguins;

    /// <summary>
    /// Initialize the renderer.
    /// </summary>
    public void Initialize(int width, int height)
    {
        _renderer.Initialize(width, height);
    }

    /// <summary>
    /// Render the scene.
    /// </summary>
    public void Render(System.Numerics.Vector2 cursorPosition)
    {
        // Use the renderer's internal bitmap and canvas directly
        // Avoids creating a new canvas every frame
        var bitmap = _renderer.RenderBitmap;
        var canvas = _renderer.Canvas;
        if (bitmap != null && canvas != null)
        {
            _renderer.Render(cursorPosition, bitmap, canvas);
        }
    }

    /// <summary>
    /// Update the animation state.
    /// </summary>
    public void Update(float deltaTime)
    {
        _animation.Update(deltaTime);
        _npcManager.Update(deltaTime);
    }

    /// <summary>
    /// Move Pingu to a target position.
    /// </summary>
    public void MovePinguTo(float x, float y, float duration = 2f)
    {
        _npcManager.MoveTo(Pingu, x, y, duration);
    }

    /// <summary>
    /// Set Pingu's role.
    /// </summary>
    public void SetPinguRole(PinguRole role)
    {
        _npcManager.SetRole(Pingu, role);
    }

    /// <summary>
    /// Equip a tool to Pingu.
    /// </summary>
    public void EquipPinguTool(PinguToolType tool)
    {
        _npcManager.EquipTool(Pingu, tool);
    }

    /// <summary>
    /// Queue a task for Pingu.
    /// </summary>
    public void QueuePinguTask(Domain.Models.PinguTask task)
    {
        _npcManager.QueueTask(Pingu, task);
    }

    /// <summary>
    /// Add a guest penguin.
    /// </summary>
    public PinguNPC AddGuestPingu(string name = "Guest")
    {
        return _npcManager.AddGuestPingu(name);
    }

    /// <summary>
    /// Remove a penguin.
    /// </summary>
    public void RemovePingu(PinguNPC pingu)
    {
        _npcManager.RemovePingu(pingu);
    }

    /// <summary>
    /// Trigger a random twitch.
    /// </summary>
    public void TriggerTwitch()
    {
        _animation.TriggerTwitch();
    }

    /// <summary>
    /// Trigger a head turn.
    /// </summary>
    public void TriggerHeadTurn()
    {
        _animation.TriggerHeadTurn();
    }

    /// <summary>
    /// Trigger a scratch.
    /// </summary>
    public void TriggerScratch()
    {
        _animation.TriggerScratch();
    }

    /// <summary>
    /// Trigger a blink.
    /// </summary>
    public void TriggerBlink()
    {
        _animation.TriggerBlink();
    }

    /// <summary>
    /// Trigger sitting down.
    /// </summary>
    public void TriggerSitDown()
    {
        _animation.TriggerSitDown();
    }

    /// <summary>
    /// Trigger sitting up.
    /// </summary>
    public void TriggerSitUp()
    {
        _animation.TriggerSitUp();
    }

    /// <summary>
    /// Dispose the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _renderer?.Dispose();
    }
}

#endregion