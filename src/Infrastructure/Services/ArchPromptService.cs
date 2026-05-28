using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Provides architecture-specific system prompts and cross-compile variables for QEMU VMs.
/// </summary>
public class ArchPromptService : IArchPromptService
{
    private static readonly Dictionary<ArchitectureType, string> _systemPrompts = new()
    {
        [ArchitectureType.X86_64] = """
            You are assisting with x86_64 Linux VM development.
            Build: cmake -B build -DGGML_XNNPACK=ON && cmake --build build --config Release
            CC=gcc CXX=g++
            Known issues: SMP locks on AMD, segfaults on some kernels
            Use raw disk images (qcow2 not supported on microvm)
            """,

        [ArchitectureType.AArch64] = """
            You are assisting with ARM64 Linux VM development.
            Build: AArch64 cross-compile with aarch64-linux-gnu toolchain
            Use EDK2 UEFI firmware for boot
            virtio-net-pci NIC model
            """,

        [ArchitectureType.RISC_V64] = """
            You are assisting with RISC-V64 MicroVM development.
            Build: riscv64-linux-gnu toolchain
            Use EDK2 UEFI firmware
            SBI firmware required for flash
            No -bios flag for eMMC flash
            """,

        [ArchitectureType.AVR] = """
            You are assisting with AVR bare-metal MCU development.
            No -machine flag, no disk images
            Compile with avr-gcc
            Flash via -bios firmware.hex
            """,

        [ArchitectureType.MIPS] = """
            You are assisting with MIPS Malta board development.
            Use tcsh shell, CMAKE_TARGET_ARCHITECTURE=mips
            """,

        [ArchitectureType.MIPS64] = """
            You are assisting with MIPS64 Malta board development.
            Use tcsh shell, CMAKE_TARGET_ARCHITECTURE=mips64
            """,

        [ArchitectureType.MIPSEL] = """
            You are assisting with MIPSEL Malta board development.
            Use tcsh shell, CMAKE_TARGET_ARCHITECTURE=mipsel
            """,

        [ArchitectureType.MIPS64EL] = """
            You are assisting with MIPS64EL Malta board development.
            Use tcsh shell, CMAKE_TARGET_ARCHITECTURE=mips64el
            """,

        [ArchitectureType.PPC] = """
            You are assisting with PowerPC Embedded development.
            Use RSPS64, no SMP support
            PReP firmware via -bios
            """,

        [ArchitectureType.PPC64] = """
            You are assisting with PowerPC 64 development.
            Use spufs, no SMP support
            """,

        [ArchitectureType.SPARC] = """
            You are assisting with SPARCstation 10 development.
            Use GCC_SPARC, known issues with floating point
            """,

        [ArchitectureType.SPARC64] = """
            You are assisting with SPARC64 LEON3 development.
            Known issues with floating point operations
            """,

        [ArchitectureType.I386] = """
            You are assisting with i386 development.
            Use SeaBIOS (-bios) and pflash disk images
            """,

        [ArchitectureType.ARMv7L] = """
            You are assisting with ARMv7L development.
            Use virtio-net-pci NIC, EDK2 UEFI firmware
            """,
    };

    private static readonly Dictionary<ArchitectureType, string> _crossCompileEnv = new()
    {
        [ArchitectureType.X86_64] = "CC=gcc CXX=g++",
        [ArchitectureType.I386] = "CC=i386-linux-gnu-gcc CXX=i386-linux-gnu-g++",
        [ArchitectureType.AArch64] = "CC=aarch64-linux-gnu-gcc CXX=aarch64-linux-gnu-g++",
        [ArchitectureType.ARMv7L] = "CC=arm-linux-gnueabihf-gcc CXX=arm-linux-gnueabihf-g++",
        [ArchitectureType.RISC_V64] = "CC=riscv64-linux-gnu-gcc CXX=riscv64-linux-gnu-g++",
        [ArchitectureType.RISC_V32] = "CC=riscv32-linux-gnu-gcc CXX=riscv32-linux-gnu-g++",
        [ArchitectureType.AVR] = "AVR_TOOLCHAIN=avr-gcc",
        [ArchitectureType.MIPS] = "CC=mips-linux-gnu-gcc CXX=mips-linux-gnu-g++",
        [ArchitectureType.MIPS64] = "CC=mips64-linux-gnu-gcc CXX=mips64-linux-gnu-g++",
        [ArchitectureType.MIPSEL] = "CC=mipsel-linux-gnu-gcc CXX=mipsel-linux-gnu-g++",
        [ArchitectureType.MIPS64EL] = "CC=mips64el-linux-gnu-gcc CXX=mips64el-linux-gnu-g++",
        [ArchitectureType.PPC] = "CC=powerpc-linux-gnu-gcc CXX=powerpc-linux-gnu-g++",
        [ArchitectureType.PPC64] = "CC=powerpc64-linux-gnu-gcc CXX=powerpc64-linux-gnu-g++",
        [ArchitectureType.SPARC] = "CC=sparc-linux-gnu-gcc CXX=sparc-linux-gnu-g++",
        [ArchitectureType.SPARC64] = "CC=sparc64-linux-gnu-gcc CXX=sparc64-linux-gnu-g++",
    };

    public string GetSystemPrompt(ArchitectureType arch) =>
        _systemPrompts.GetValueOrDefault(arch, "");

    public string GetCrossCompileEnvVars(ArchitectureType arch) =>
        _crossCompileEnv.GetValueOrDefault(arch, "");
}
