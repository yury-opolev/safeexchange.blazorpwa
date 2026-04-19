# Attachments Integrity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** End-to-end attachment integrity — per-chunk SHA-256 verified at upload, whole-content hash committed explicitly, download-side verification with auto-abort on mismatch, UI display + local-file verify.

**Architecture:** Three layers (per-chunk hash at upload, whole-content hash at commit, whole-content hash at download). Cross-repo: client (`safeexchange.blazorpwa`, branch `features/attachments-integrity`) + backend (`safeexchange`, branch `features/attachments-integrity` — to be created). Rollout guarded by two `Features` flags; backend deploys first.

**Tech Stack:** .NET 10 / Blazor WebAssembly PWA, Azure Functions isolated worker, EF Core (SQL Server), xUnit for .NET tests, Playwright for E2E, Bootstrap 5 + `bi-*` icons for UI.

**Design spec:** `docs/superpowers/specs/2026-04-19-attachments-integrity-design.md` (in client repo).

**Test user for staging:** `test.2.user@spaceoysteroutlook.onmicrosoft.com` — credentials in session context.

---

## Conventions

- **Backend repo root:** `C:\Users\yurio\Documents\github\safeexchange`
- **Client repo root:** `C:\Users\yurio\Documents\github\safeexchange.blazorpwa`
- **All shell paths** below are relative to the repo root unless absolute.
- **Test names** for xUnit: `Method_State_Expected` convention used by existing tests.
- **Every task ends with a commit** using Conventional Commits style.
- **C# style** follows the user's global CLAUDE.md rules (`this.` prefix, curly braces always, camelCase private fields, `ConfigureAwait(false)` in library code, etc.).
- **Tests-first** for every production-code task.

---

## Phase 0: Setup

### Task 0.1: Create backend feature branch

**Files:**
- None — git plumbing.

- [ ] **Step 1: Create branch in backend repo**

```bash
cd /c/Users/yurio/Documents/github/safeexchange
git checkout main
git pull
git checkout -b features/attachments-integrity
git status
```

Expected: clean tree, on `features/attachments-integrity`, tracking `main`.

- [ ] **Step 2: Verify client branch state**

```bash
cd /c/Users/yurio/Documents/github/safeexchange.blazorpwa
git branch --show-current
git log --oneline -3
```

Expected: on `features/attachments-integrity`, latest commit is the spec `dc2604c`.

---

## Phase 1: Hand-rolled `SerializableSha256` (backend, highest-stakes)

### Task 1.1: Create `SerializableSha256` skeleton with empty methods

**Files:**
- Create: `SafeExchange.Core/Crypto/SerializableSha256.cs`

- [ ] **Step 1: Create the file with stub API**

```csharp
/// <summary>
/// Incremental SHA-256 with serialisable state for multi-request hashing.
/// Follows FIPS 180-4. Cross-checked against System.Security.Cryptography.SHA256
/// in every test (see SerializableSha256Tests).
/// </summary>

namespace SafeExchange.Core.Crypto;

using System;

public sealed class SerializableSha256
{
    public const int StateSize = 106;

    public SerializableSha256() => throw new NotImplementedException();

    public void Append(ReadOnlySpan<byte> data) => throw new NotImplementedException();

    public byte[] Finish() => throw new NotImplementedException();

    public byte[] SaveState() => throw new NotImplementedException();

    public static SerializableSha256 Restore(byte[] state) => throw new NotImplementedException();
}
```

- [ ] **Step 2: Build to confirm the file compiles**

```bash
cd /c/Users/yurio/Documents/github/safeexchange
dotnet build SafeExchange.Core/SafeExchange.Core.csproj -c Release --nologo -v minimal
```

Expected: `0 Error(s)`.

- [ ] **Step 3: Commit skeleton**

```bash
git add SafeExchange.Core/Crypto/SerializableSha256.cs
git commit -m "feat(integrity): add SerializableSha256 skeleton"
```

### Task 1.2: NIST FIPS 180-4 test vectors

**Files:**
- Create: `SafeExchange.Tests/Tests/SerializableSha256Tests.cs`

- [ ] **Step 1: Write the failing test file**

```csharp
/// <summary>
/// SerializableSha256 tests — NIST FIPS 180-4 vectors + BCL cross-check +
/// state-serialisation properties.
/// </summary>

namespace SafeExchange.Tests.Tests;

using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SafeExchange.Core.Crypto;

[TestClass]
public class SerializableSha256Tests
{
    // FIPS 180-4 test vectors.
    private const string EmptyHex = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string AbcHex = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [TestMethod]
    public void HashAll_EmptyInput_MatchesFipsVector()
    {
        var hash = Convert.ToHexString(HashAll(Array.Empty<byte>())).ToLowerInvariant();
        Assert.AreEqual(EmptyHex, hash);
    }

    [TestMethod]
    public void HashAll_Abc_MatchesFipsVector()
    {
        var hash = Convert.ToHexString(HashAll(Encoding.ASCII.GetBytes("abc"))).ToLowerInvariant();
        Assert.AreEqual(AbcHex, hash);
    }

    [TestMethod]
    public void HashAll_448BitMessage_MatchesFipsVector()
    {
        // "abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq" (56 bytes)
        var input = Encoding.ASCII.GetBytes("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq");
        var hash = Convert.ToHexString(HashAll(input)).ToLowerInvariant();
        Assert.AreEqual("248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1", hash);
    }

    [TestMethod]
    public void HashAll_OneMillionAs_MatchesFipsVector()
    {
        var input = new byte[1_000_000];
        Array.Fill(input, (byte)'a');
        var hash = Convert.ToHexString(HashAll(input)).ToLowerInvariant();
        Assert.AreEqual("cdc76e5c9914fb9281a1c7e284d73e67f1809a48a497200e046d39ccc7112cd0", hash);
    }

    private static byte[] HashAll(byte[] data)
    {
        var hasher = new SerializableSha256();
        hasher.Append(data);
        return hasher.Finish();
    }
}
```

- [ ] **Step 2: Run to verify tests fail**

```bash
cd /c/Users/yurio/Documents/github/safeexchange
dotnet test SafeExchange.Tests/SafeExchange.Tests.csproj --filter "FullyQualifiedName~SerializableSha256Tests" --nologo
```

Expected: FAILED with `NotImplementedException`.

- [ ] **Step 3: Commit failing tests**

```bash
git add SafeExchange.Tests/Tests/SerializableSha256Tests.cs
git commit -m "test(integrity): add NIST SHA-256 vectors for SerializableSha256"
```

### Task 1.3: Implement SHA-256 core algorithm

**Files:**
- Modify: `SafeExchange.Core/Crypto/SerializableSha256.cs` (replace stubs with real impl)

- [ ] **Step 1: Replace the stub body with the full FIPS 180-4 implementation**

```csharp
/// <summary>
/// Incremental SHA-256 with serialisable state for multi-request hashing.
/// Follows FIPS 180-4. Cross-checked against System.Security.Cryptography.SHA256
/// in every test (see SerializableSha256Tests).
///
/// State binary format (version 1, 106 bytes):
///   offset 0,   1 byte:   version marker (0x01)
///   offset 1,   32 bytes: H0..H7 as 8 uint32 big-endian
///   offset 33,  1 byte:   partial-block byte count (0..63)
///   offset 34,  64 bytes: partial-block buffer (zero-padded if partial)
///   offset 98,  8 bytes:  total bit count as uint64 big-endian
/// </summary>

namespace SafeExchange.Core.Crypto;

using System;
using System.Buffers.Binary;

public sealed class SerializableSha256
{
    public const int StateSize = 106;
    private const byte StateVersion = 0x01;
    private const int BlockSize = 64;

    private static readonly uint[] InitialH = new uint[]
    {
        0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
        0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19,
    };

    private static readonly uint[] K = new uint[]
    {
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
    };

    private readonly uint[] h;
    private readonly byte[] partial;
    private int partialLength;
    private ulong totalBits;

    public SerializableSha256()
    {
        this.h = (uint[])InitialH.Clone();
        this.partial = new byte[BlockSize];
        this.partialLength = 0;
        this.totalBits = 0;
    }

    public void Append(ReadOnlySpan<byte> data)
    {
        this.totalBits += (ulong)data.Length * 8UL;

        if (this.partialLength > 0)
        {
            var need = BlockSize - this.partialLength;
            if (data.Length < need)
            {
                data.CopyTo(this.partial.AsSpan(this.partialLength));
                this.partialLength += data.Length;
                return;
            }

            data.Slice(0, need).CopyTo(this.partial.AsSpan(this.partialLength));
            this.ProcessBlock(this.partial);
            this.partialLength = 0;
            data = data.Slice(need);
        }

        while (data.Length >= BlockSize)
        {
            this.ProcessBlock(data.Slice(0, BlockSize));
            data = data.Slice(BlockSize);
        }

        if (data.Length > 0)
        {
            data.CopyTo(this.partial.AsSpan());
            this.partialLength = data.Length;
        }
    }

    public byte[] Finish()
    {
        Span<byte> padBuffer = stackalloc byte[BlockSize * 2];
        var bitLen = this.totalBits;

        this.partial.AsSpan(0, this.partialLength).CopyTo(padBuffer);
        padBuffer[this.partialLength] = 0x80;
        var padEnd = this.partialLength + 1;

        var totalNeeded = padEnd + 8;
        var paddedLen = totalNeeded <= BlockSize ? BlockSize : BlockSize * 2;
        padBuffer.Slice(padEnd, paddedLen - padEnd - 8).Clear();
        BinaryPrimitives.WriteUInt64BigEndian(padBuffer.Slice(paddedLen - 8, 8), bitLen);

        this.ProcessBlock(padBuffer.Slice(0, BlockSize));
        if (paddedLen == BlockSize * 2)
        {
            this.ProcessBlock(padBuffer.Slice(BlockSize, BlockSize));
        }

        var digest = new byte[32];
        for (var i = 0; i < 8; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(digest.AsSpan(i * 4, 4), this.h[i]);
        }

        return digest;
    }

    public byte[] SaveState()
    {
        var state = new byte[StateSize];
        state[0] = StateVersion;
        for (var i = 0; i < 8; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(state.AsSpan(1 + i * 4, 4), this.h[i]);
        }

        state[33] = (byte)this.partialLength;
        this.partial.AsSpan(0, this.partialLength).CopyTo(state.AsSpan(34));
        if (this.partialLength < BlockSize)
        {
            state.AsSpan(34 + this.partialLength, BlockSize - this.partialLength).Clear();
        }

        BinaryPrimitives.WriteUInt64BigEndian(state.AsSpan(98, 8), this.totalBits);
        return state;
    }

    public static SerializableSha256 Restore(byte[] state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (state.Length != StateSize)
        {
            throw new ArgumentException($"State length must be {StateSize}.", nameof(state));
        }

        if (state[0] != StateVersion)
        {
            throw new ArgumentException($"Unsupported state version: {state[0]:X2}.", nameof(state));
        }

        var instance = new SerializableSha256();
        for (var i = 0; i < 8; i++)
        {
            instance.h[i] = BinaryPrimitives.ReadUInt32BigEndian(state.AsSpan(1 + i * 4, 4));
        }

        instance.partialLength = state[33];
        if (instance.partialLength < 0 || instance.partialLength >= BlockSize)
        {
            throw new ArgumentException("Invalid partial length in state.", nameof(state));
        }

        state.AsSpan(34, BlockSize).CopyTo(instance.partial);
        instance.totalBits = BinaryPrimitives.ReadUInt64BigEndian(state.AsSpan(98, 8));
        return instance;
    }

    private void ProcessBlock(ReadOnlySpan<byte> block)
    {
        Span<uint> w = stackalloc uint[64];
        for (var i = 0; i < 16; i++)
        {
            w[i] = BinaryPrimitives.ReadUInt32BigEndian(block.Slice(i * 4, 4));
        }

        for (var i = 16; i < 64; i++)
        {
            var s0 = RotateRight(w[i - 15], 7) ^ RotateRight(w[i - 15], 18) ^ (w[i - 15] >> 3);
            var s1 = RotateRight(w[i - 2], 17) ^ RotateRight(w[i - 2], 19) ^ (w[i - 2] >> 10);
            w[i] = w[i - 16] + s0 + w[i - 7] + s1;
        }

        var a = this.h[0];
        var b = this.h[1];
        var c = this.h[2];
        var d = this.h[3];
        var e = this.h[4];
        var f = this.h[5];
        var g = this.h[6];
        var hh = this.h[7];

        for (var i = 0; i < 64; i++)
        {
            var s1 = RotateRight(e, 6) ^ RotateRight(e, 11) ^ RotateRight(e, 25);
            var ch = (e & f) ^ (~e & g);
            var temp1 = hh + s1 + ch + K[i] + w[i];
            var s0 = RotateRight(a, 2) ^ RotateRight(a, 13) ^ RotateRight(a, 22);
            var maj = (a & b) ^ (a & c) ^ (b & c);
            var temp2 = s0 + maj;

            hh = g;
            g = f;
            f = e;
            e = d + temp1;
            d = c;
            c = b;
            b = a;
            a = temp1 + temp2;
        }

        this.h[0] += a;
        this.h[1] += b;
        this.h[2] += c;
        this.h[3] += d;
        this.h[4] += e;
        this.h[5] += f;
        this.h[6] += g;
        this.h[7] += hh;
    }

    private static uint RotateRight(uint value, int bits) => (value >> bits) | (value << (32 - bits));
}
```

- [ ] **Step 2: Run tests to verify they pass**

```bash
dotnet test SafeExchange.Tests/SafeExchange.Tests.csproj --filter "FullyQualifiedName~SerializableSha256Tests" --nologo
```

Expected: 4 / 4 passed.

- [ ] **Step 3: Commit**

```bash
git add SafeExchange.Core/Crypto/SerializableSha256.cs
git commit -m "feat(integrity): implement SerializableSha256 with FIPS 180-4 vectors passing"
```

### Task 1.4: BCL cross-check + state-serialisation property tests

**Files:**
- Modify: `SafeExchange.Tests/Tests/SerializableSha256Tests.cs`

- [ ] **Step 1: Add BCL cross-check + split-point property + edge-case tests**

Append to the class (before the closing `}`):

```csharp
    [TestMethod]
    public void HashAll_BoundaryLengths_MatchBcl()
    {
        int[] lengths = new[] { 0, 1, 55, 56, 63, 64, 65, 127, 128, 129, 4096 };
        foreach (var length in lengths)
        {
            var input = new byte[length];
            for (var i = 0; i < length; i++)
            {
                input[i] = (byte)(i & 0xff);
            }

            var ours = HashAll(input);
            var bcl = SHA256.HashData(input);
            CollectionAssert.AreEqual(bcl, ours, $"Divergence from BCL at length {length}.");
        }
    }

    [TestMethod]
    public void Append_SplitAtEverySplitPoint_MatchesWholeHash()
    {
        var rng = new Random(42);
        for (var iteration = 0; iteration < 64; iteration++)
        {
            var length = rng.Next(1, 4096);
            var input = new byte[length];
            rng.NextBytes(input);
            var expected = SHA256.HashData(input);

            for (var splitPoint = 0; splitPoint <= length; splitPoint++)
            {
                var hasher = new SerializableSha256();
                hasher.Append(input.AsSpan(0, splitPoint));
                hasher.Append(input.AsSpan(splitPoint));
                CollectionAssert.AreEqual(expected, hasher.Finish(),
                    $"Divergence at split {splitPoint}/{length}, iteration {iteration}.");
            }
        }
    }

    [TestMethod]
    public void SaveState_RestoreAtEverySplitPoint_MatchesWholeHash()
    {
        var rng = new Random(123);
        for (var iteration = 0; iteration < 64; iteration++)
        {
            var length = rng.Next(1, 4096);
            var input = new byte[length];
            rng.NextBytes(input);
            var expected = SHA256.HashData(input);

            for (var splitPoint = 0; splitPoint <= length; splitPoint++)
            {
                var first = new SerializableSha256();
                first.Append(input.AsSpan(0, splitPoint));
                var state = first.SaveState();

                var second = SerializableSha256.Restore(state);
                second.Append(input.AsSpan(splitPoint));
                CollectionAssert.AreEqual(expected, second.Finish(),
                    $"Save/restore divergence at split {splitPoint}/{length}, iteration {iteration}.");
            }
        }
    }

    [TestMethod]
    public void SaveRestoreSaveRestore_DoesNotCorruptState()
    {
        var rng = new Random(7);
        var input = new byte[8192];
        rng.NextBytes(input);
        var expected = SHA256.HashData(input);

        var hasher = new SerializableSha256();
        var pos = 0;
        while (pos < input.Length)
        {
            var step = Math.Min(rng.Next(1, 256), input.Length - pos);
            hasher.Append(input.AsSpan(pos, step));
            var state = hasher.SaveState();
            hasher = SerializableSha256.Restore(state);
            pos += step;
        }

        CollectionAssert.AreEqual(expected, hasher.Finish());
    }

    [TestMethod]
    public void Restore_InvalidLength_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() => SerializableSha256.Restore(new byte[105]));
    }

    [TestMethod]
    public void Restore_InvalidVersion_Throws()
    {
        var state = new SerializableSha256().SaveState();
        state[0] = 0x99;
        Assert.ThrowsException<ArgumentException>(() => SerializableSha256.Restore(state));
    }

    [TestMethod]
    public void Restore_Null_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => SerializableSha256.Restore(null!));
    }

    [TestMethod]
    public void HashAll_Fuzz100kRandomInputs_MatchesBcl()
    {
        var rng = new Random(9001);
        for (var iteration = 0; iteration < 50; iteration++)
        {
            var length = rng.Next(1, 100_000);
            var input = new byte[length];
            rng.NextBytes(input);
            var ours = HashAll(input);
            var bcl = SHA256.HashData(input);
            CollectionAssert.AreEqual(bcl, ours, $"Divergence at iteration {iteration}, length {length}.");
        }
    }
}
```

- [ ] **Step 2: Run all SerializableSha256 tests**

```bash
dotnet test SafeExchange.Tests/SafeExchange.Tests.csproj --filter "FullyQualifiedName~SerializableSha256Tests" --nologo
```

Expected: all tests pass (original 4 + 8 new = 12).

- [ ] **Step 3: Commit**

```bash
git add SafeExchange.Tests/Tests/SerializableSha256Tests.cs
git commit -m "test(integrity): add BCL parity + save/restore property tests for SerializableSha256"
```

---

## Phase 2: Backend data model

### Task 2.1: Extend `ContentMetadata` model with integrity columns

**Files:**
- Modify: `SafeExchange.Core/Model/ContentMetadata.cs`

- [ ] **Step 1: Read the existing file**

```bash
cat SafeExchange.Core/Model/ContentMetadata.cs
```

- [ ] **Step 2: Add two new public properties**

Inside the class body, after the existing `Chunks` property (exact insertion location: before the class's closing brace), add:

```csharp
        /// <summary>
        /// Lowercase hex SHA-256 of the full attachment content, set at commit.
        /// Null / empty for legacy (pre-integrity-feature) content and for IsMain==true content.
        /// </summary>
        public string? Hash { get; set; }

        /// <summary>
        /// Serialised SerializableSha256 state persisted across chunk-upload HTTP requests.
        /// Non-null only during an active hashed-mode upload; cleared on commit, on purge,
        /// or on access-ticket expiry.
        /// </summary>
        public byte[]? RunningHashState { get; set; }
```

- [ ] **Step 3: Build**

```bash
dotnet build SafeExchange.Core/SafeExchange.Core.csproj -c Release --nologo -v minimal
```

Expected: `0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add SafeExchange.Core/Model/ContentMetadata.cs
git commit -m "feat(integrity): add Hash + RunningHashState columns to ContentMetadata"
```

### Task 2.2: Add the EF Core migration item

**Files:**
- Create: `SafeExchange.Core/Migrations/Model/MigrationItem00008.cs`

- [ ] **Step 1: Read the most recent migration item for the pattern**

```bash
cat SafeExchange.Core/Migrations/Model/MigrationItem00007.cs | head -40
```

- [ ] **Step 2: Create the new migration item following the established pattern**

Write the new migration that performs `ALTER TABLE` to add both columns. Mirror the structure of MigrationItem00007.cs exactly — same namespace, same base class, same properties — with the SQL commands targeting `ContentMetadata` (or the actual table name revealed by step 1).

Body template (adjust the class name + Up/Down payloads to match the established pattern):

```csharp
namespace SafeExchange.Core.Migrations.Model;

using System;

public class MigrationItem00008 : IMigrationItem
{
    public string Name => nameof(MigrationItem00008);

    public int Order => 8;

    public string Description => "Add Hash and RunningHashState columns to ContentMetadata for attachment integrity.";

    public async Task ApplyAsync(SafeExchangeDbContext dbContext)
    {
        // Adjust to whatever mechanism MigrationItem00007 uses — raw SQL via
        // dbContext.Database.ExecuteSqlRawAsync(...), or model rebuild, etc.
    }
}
```

**Note to implementer:** the exact mechanism (`ExecuteSqlRawAsync` vs. programmatic `MigrationBuilder`) depends on the existing convention. Inspect `MigrationItem00007.cs` and `IMigrationItem.cs` before writing. If raw SQL is used elsewhere, add:

```sql
ALTER TABLE ContentMetadata ADD Hash NVARCHAR(64) NULL;
ALTER TABLE ContentMetadata ADD RunningHashState VARBINARY(128) NULL;
```

Register the new item wherever `MigrationsHelper` assembles the sequence (grep for `MigrationItem00007` in `MigrationsHelper.cs`).

- [ ] **Step 3: Build**

```bash
dotnet build SafeExchange.Core/SafeExchange.Core.csproj -c Release --nologo -v minimal
```

Expected: `0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add SafeExchange.Core/Migrations/Model/MigrationItem00008.cs SafeExchange.Core/Migrations/MigrationsHelper.cs
git commit -m "feat(integrity): migration 00008 adds Hash + RunningHashState columns"
```

### Task 2.3: Extend output DTOs

**Files:**
- Modify: `SafeExchange.Core/Model/Dto/Output/ContentMetadataOutput.cs`
- Modify: `SafeExchange.Core/Model/ContentMetadata.cs` (ToDto mapping)

- [ ] **Step 1: Add `Hash` to the output DTO**

Read the existing `ContentMetadataOutput.cs` and add a nullable `Hash` property in the same style as the existing properties. Update any `ToDto()` method on `ContentMetadata` to populate it.

- [ ] **Step 2: Build**

```bash
dotnet build SafeExchange.Core/SafeExchange.Core.csproj -c Release --nologo -v minimal
```

Expected: `0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add SafeExchange.Core/Model/Dto/Output/ContentMetadataOutput.cs SafeExchange.Core/Model/ContentMetadata.cs
git commit -m "feat(integrity): surface ContentMetadata.Hash in output DTO"
```

---

## Phase 3: Backend Features config flags

### Task 3.1: Add flags to `Features`

**Files:**
- Modify: `SafeExchange.Core/Configuration/Features.cs`

- [ ] **Step 1: Edit the `Features` class**

Add two bool properties and update `Clone()`, `Equals()`, and `GetHashCode()`:

```csharp
        public bool AllowLegacyAttachmentUploads { get; set; } = true;

        public bool IgnoreChunkHashHeader { get; set; } = false;
```

Clone:
```csharp
                AllowLegacyAttachmentUploads = this.AllowLegacyAttachmentUploads,
                IgnoreChunkHashHeader = this.IgnoreChunkHashHeader,
```

Equals: add corresponding `&&` clauses.

GetHashCode: update `HashCode.Combine` to include the new fields.

- [ ] **Step 2: Build**

```bash
dotnet build SafeExchange.Core/SafeExchange.Core.csproj -c Release --nologo -v minimal
```

Expected: `0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add SafeExchange.Core/Configuration/Features.cs
git commit -m "feat(integrity): add AllowLegacyAttachmentUploads + IgnoreChunkHashHeader flags"
```

---

## Phase 4: Backend upload-mode inference

### Task 4.1: Write tests for `UploadModeResolver`

**Files:**
- Create: `SafeExchange.Tests/Tests/UploadModeResolverTests.cs`

- [ ] **Step 1: Write the failing test file**

```csharp
namespace SafeExchange.Tests.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SafeExchange.Core.Model;
using SafeExchange.Core.Stream;

[TestClass]
public class UploadModeResolverTests
{
    [TestMethod]
    public void Resolve_HeaderPresent_NoPriorChunks_ReturnsHashed()
    {
        var content = NewContent();
        var mode = UploadModeResolver.Resolve(content, hashHeaderPresent: true, allowLegacy: true, ignoreHeader: false);
        Assert.AreEqual(UploadMode.Hashed, mode);
    }

    [TestMethod]
    public void Resolve_HeaderPresent_HashedModeLocked_ReturnsHashed()
    {
        var content = NewContent();
        content.RunningHashState = new byte[1];
        var mode = UploadModeResolver.Resolve(content, hashHeaderPresent: true, allowLegacy: true, ignoreHeader: false);
        Assert.AreEqual(UploadMode.Hashed, mode);
    }

    [TestMethod]
    public void Resolve_HeaderPresent_LegacyModeLocked_ReturnsReject()
    {
        var content = NewContent();
        content.Chunks.Add(new ChunkMetadata { ChunkName = "x", Hash = "", Length = 10 });
        // RunningHashState stays null -> legacy locked
        var mode = UploadModeResolver.Resolve(content, hashHeaderPresent: true, allowLegacy: true, ignoreHeader: false);
        Assert.AreEqual(UploadMode.Reject, mode);
    }

    [TestMethod]
    public void Resolve_HeaderAbsent_FlagOn_NoPriorChunks_ReturnsLegacy()
    {
        var content = NewContent();
        var mode = UploadModeResolver.Resolve(content, hashHeaderPresent: false, allowLegacy: true, ignoreHeader: false);
        Assert.AreEqual(UploadMode.Legacy, mode);
    }

    [TestMethod]
    public void Resolve_HeaderAbsent_FlagOff_ReturnsReject()
    {
        var content = NewContent();
        var mode = UploadModeResolver.Resolve(content, hashHeaderPresent: false, allowLegacy: false, ignoreHeader: false);
        Assert.AreEqual(UploadMode.Reject, mode);
    }

    [TestMethod]
    public void Resolve_HeaderAbsent_HashedModeLocked_ReturnsReject()
    {
        var content = NewContent();
        content.RunningHashState = new byte[1];
        var mode = UploadModeResolver.Resolve(content, hashHeaderPresent: false, allowLegacy: true, ignoreHeader: false);
        Assert.AreEqual(UploadMode.Reject, mode);
    }

    [TestMethod]
    public void Resolve_IgnoreHeaderFlag_ForcesLegacy()
    {
        var content = NewContent();
        var mode = UploadModeResolver.Resolve(content, hashHeaderPresent: true, allowLegacy: true, ignoreHeader: true);
        Assert.AreEqual(UploadMode.Legacy, mode);
    }

    private static ContentMetadata NewContent() => new() { ContentName = "c-00000000" };
}
```

- [ ] **Step 2: Run to verify tests fail**

```bash
dotnet test SafeExchange.Tests/SafeExchange.Tests.csproj --filter "FullyQualifiedName~UploadModeResolverTests" --nologo
```

Expected: build error — `UploadModeResolver` does not exist yet.

- [ ] **Step 3: Commit failing tests**

```bash
git add SafeExchange.Tests/Tests/UploadModeResolverTests.cs
git commit -m "test(integrity): spec UploadModeResolver behaviour via tests"
```

### Task 4.2: Implement `UploadModeResolver`

**Files:**
- Create: `SafeExchange.Core/Stream/UploadMode.cs`
- Create: `SafeExchange.Core/Stream/UploadModeResolver.cs`

- [ ] **Step 1: Create the enum**

```csharp
namespace SafeExchange.Core.Stream;

public enum UploadMode
{
    Reject,
    Legacy,
    Hashed,
}
```

- [ ] **Step 2: Create the resolver**

```csharp
namespace SafeExchange.Core.Stream;

using SafeExchange.Core.Model;

public static class UploadModeResolver
{
    public static UploadMode Resolve(
        ContentMetadata content,
        bool hashHeaderPresent,
        bool allowLegacy,
        bool ignoreHeader)
    {
        if (ignoreHeader)
        {
            return allowLegacy ? UploadMode.Legacy : UploadMode.Reject;
        }

        var hashedModeLocked = content.RunningHashState is { Length: > 0 };
        var legacyModeLocked = !hashedModeLocked && content.Chunks.Count > 0;

        if (hashHeaderPresent)
        {
            return legacyModeLocked ? UploadMode.Reject : UploadMode.Hashed;
        }

        if (hashedModeLocked)
        {
            return UploadMode.Reject;
        }

        return allowLegacy ? UploadMode.Legacy : UploadMode.Reject;
    }
}
```

- [ ] **Step 3: Run tests to verify they pass**

```bash
dotnet test SafeExchange.Tests/SafeExchange.Tests.csproj --filter "FullyQualifiedName~UploadModeResolverTests" --nologo
```

Expected: 7 / 7 passed.

- [ ] **Step 4: Commit**

```bash
git add SafeExchange.Core/Stream/UploadMode.cs SafeExchange.Core/Stream/UploadModeResolver.cs
git commit -m "feat(integrity): UploadModeResolver decides hashed/legacy/reject per request"
```

---

## Phase 5: Backend tee-hashing stream

### Task 5.1: Write tests for `HashingReadStream`

**Files:**
- Create: `SafeExchange.Tests/Tests/HashingReadStreamTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
namespace SafeExchange.Tests.Tests;

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SafeExchange.Core.Crypto;
using SafeExchange.Core.Stream;

[TestClass]
public class HashingReadStreamTests
{
    [TestMethod]
    public async Task Read_ForwardsBytesAndHashesBoth()
    {
        var payload = Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog");
        using var source = new MemoryStream(payload);
        var chunkHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var running = new SerializableSha256();
        await using var tee = new HashingReadStream(source, chunkHasher, running);

        using var sink = new MemoryStream();
        await tee.CopyToAsync(sink);

        CollectionAssert.AreEqual(payload, sink.ToArray());
        var chunkHash = chunkHasher.GetHashAndReset();
        CollectionAssert.AreEqual(SHA256.HashData(payload), chunkHash);
        CollectionAssert.AreEqual(SHA256.HashData(payload), running.Finish());
    }

    [TestMethod]
    public async Task Read_ChunksOfRandomSize_StillHashesCorrectly()
    {
        var rng = new System.Random(42);
        var payload = new byte[10_000];
        rng.NextBytes(payload);
        using var source = new MemoryStream(payload);
        var chunkHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var running = new SerializableSha256();
        await using var tee = new HashingReadStream(source, chunkHasher, running);

        using var sink = new MemoryStream();
        var buffer = new byte[17]; // odd size
        int read;
        while ((read = await tee.ReadAsync(buffer)) > 0)
        {
            sink.Write(buffer, 0, read);
        }

        CollectionAssert.AreEqual(payload, sink.ToArray());
        CollectionAssert.AreEqual(SHA256.HashData(payload), chunkHasher.GetHashAndReset());
        CollectionAssert.AreEqual(SHA256.HashData(payload), running.Finish());
    }
}
```

- [ ] **Step 2: Run to verify tests fail**

```bash
dotnet test SafeExchange.Tests/SafeExchange.Tests.csproj --filter "FullyQualifiedName~HashingReadStreamTests" --nologo
```

Expected: build error — `HashingReadStream` does not exist.

- [ ] **Step 3: Commit failing tests**

```bash
git add SafeExchange.Tests/Tests/HashingReadStreamTests.cs
git commit -m "test(integrity): spec HashingReadStream behaviour"
```

### Task 5.2: Implement `HashingReadStream`

**Files:**
- Create: `SafeExchange.Core/Stream/HashingReadStream.cs`

- [ ] **Step 1: Create the stream wrapper**

```csharp
namespace SafeExchange.Core.Stream;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SafeExchange.Core.Crypto;

/// <summary>
/// Read-only stream wrapper that tees every byte it yields through two hashers:
/// a per-chunk IncrementalHash and a cross-request SerializableSha256 running state.
/// Used in the chunk-upload path so blob upload and hashing happen in one pass
/// without buffering the chunk body.
/// </summary>
public sealed class HashingReadStream : Stream
{
    private readonly Stream inner;
    private readonly IncrementalHash chunkHasher;
    private readonly SerializableSha256 running;

    public HashingReadStream(Stream inner, IncrementalHash chunkHasher, SerializableSha256 running)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.chunkHasher = chunkHasher ?? throw new ArgumentNullException(nameof(chunkHasher));
        this.running = running ?? throw new ArgumentNullException(nameof(running));
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = this.inner.Read(buffer, offset, count);
        if (read > 0)
        {
            var span = new ReadOnlySpan<byte>(buffer, offset, read);
            this.chunkHasher.AppendData(buffer, offset, read);
            this.running.Append(span);
        }

        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await this.inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read > 0)
        {
            var span = buffer.Span.Slice(0, read);
            this.chunkHasher.AppendData(span);
            this.running.Append(span);
        }

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
```

- [ ] **Step 2: Run tests to verify they pass**

```bash
dotnet test SafeExchange.Tests/SafeExchange.Tests.csproj --filter "FullyQualifiedName~HashingReadStreamTests" --nologo
```

Expected: 2 / 2 passed.

- [ ] **Step 3: Commit**

```bash
git add SafeExchange.Core/Stream/HashingReadStream.cs
git commit -m "feat(integrity): HashingReadStream tees bytes into chunk + running hashers"
```

---

## Phase 6: Backend chunk upload handler revision

### Task 6.1: Extract hashed-mode upload into its own method + tests

**Files:**
- Modify: `SafeExchange.Core/Functions/SafeExchangeSecretStream.cs`
- Modify: `SafeExchange.Tests/Tests/SecretStreamTests.cs`

**Important:** `SafeExchangeSecretStream.HandleSecretStreamUpload` is a long method. Revise it to branch on `UploadModeResolver`:

- Hashed path — new method `HandleHashedChunkUpload` doing:
  1. Read `X-SafeExchange-Chunk-Hash` header.
  2. Restore `SerializableSha256` from `existingContent.RunningHashState` if present, otherwise `new SerializableSha256()`.
  3. Create `IncrementalHash` for the chunk.
  4. Wrap `request.Body` in `HashingReadStream`.
  5. `await this.blobHelper.EncryptAndUploadBlobAsync(newChunkName, hashingStream)` — bytes flow through hashers as they're read.
  6. `var actualHex = Convert.ToHexString(chunkHasher.GetHashAndReset()).ToLowerInvariant();`
  7. If `actualHex != headerHex`: delete blob, return 422 `chunk_hash_mismatch`.
  8. Else: append `new ChunkMetadata { Hash = actualHex }`, set `existingContent.RunningHashState = running.SaveState()`, save changes, return 200.
  9. **Do NOT** flip `ContentStatus.Ready` on the non-interim chunk in hashed mode.

- Legacy path — preserve existing behaviour verbatim (extract into `HandleLegacyChunkUpload` for clarity, calling the original code paths).

- Reject path — return 400 with an error indicating why (`inconsistent_upload_mode` / `bad_request`).

- [ ] **Step 1: Read existing SecretStreamTests to see patterns**

```bash
cat SafeExchange.Tests/Tests/SecretStreamTests.cs | head -80
```

- [ ] **Step 2: Add test — happy hashed-mode chunk upload with correct header → 200, ChunkMetadata.Hash populated, RunningHashState advanced**

Use the existing test-setup patterns (in-memory DB, mocked permissions, etc.). Write a test that:
1. Creates a secret + content.
2. POSTs a chunk with `X-SafeExchange-Chunk-Hash: <hex of body>` + `X-SafeExchange-OpType: interim`.
3. Asserts: 200 response, `ContentStatus.Updating` (not Ready), `Chunks.Count == 1`, `Chunks[0].Hash` is the hex, `RunningHashState != null`.

- [ ] **Step 3: Add test — chunk hash mismatch → 422, blob deleted, state unchanged**

Post a chunk with the WRONG header. Assert: 422 `chunk_hash_mismatch`, `Chunks.Count == 0`, `RunningHashState == null`, blobHelper delete was invoked.

- [ ] **Step 4: Add test — legacy-mode upload (flag on, no header) → existing behaviour preserved**

Post without header, verify `ContentStatus` flips to `Ready` on non-interim chunk and `Hash = ""`.

- [ ] **Step 5: Add test — header present but legacy-mode locked → 400**

Seed content with `Chunks.Count > 0` and `RunningHashState == null`. Post with header. Assert 400 `inconsistent_upload_mode`.

- [ ] **Step 6: Add test — header absent with flag off → 400**

Set `Features.AllowLegacyAttachmentUploads = false` in the test fixture. Post without header. Assert 400.

- [ ] **Step 7: Run tests (expect failures — implementation not updated yet)**

```bash
dotnet test SafeExchange.Tests/SafeExchange.Tests.csproj --filter "FullyQualifiedName~SecretStreamTests" --nologo
```

- [ ] **Step 8: Commit failing tests**

```bash
git add SafeExchange.Tests/Tests/SecretStreamTests.cs
git commit -m "test(integrity): spec hashed-mode + legacy-mode + reject paths for chunk upload"
```

### Task 6.2: Implement hashed-mode branch

**Files:**
- Modify: `SafeExchange.Core/Functions/SafeExchangeSecretStream.cs`

- [ ] **Step 1: Add the `ChunkHashHeaderName` constant** near the existing `AccessTicketHeaderName` / `OperationTypeHeaderName`:

```csharp
        public static readonly string ChunkHashHeaderName = "X-SafeExchange-Chunk-Hash";
```

- [ ] **Step 2: Refactor `HandleSecretStreamUpload`**

Replace the current single-path body with:

```csharp
            // existing checks (secret exists, KeepInStorage, permission, content exists,
            // chunkId empty, access ticket negotiation) — unchanged

            // NEW: read the hash header
            var hashHeader = request.Headers.TryGetValues(ChunkHashHeaderName, out var hvals)
                ? hvals.FirstOrDefault()
                : null;

            var mode = UploadModeResolver.Resolve(
                existingContent,
                hashHeaderPresent: !string.IsNullOrEmpty(hashHeader),
                allowLegacy: this.features.AllowLegacyAttachmentUploads,
                ignoreHeader: this.features.IgnoreChunkHashHeader);

            switch (mode)
            {
                case UploadMode.Reject:
                    return await ActionResults.CreateResponseAsync(
                        request, HttpStatusCode.BadRequest,
                        new BaseResponseObject<object> { Status = "inconsistent_upload_mode",
                                                        Error = "Chunk-hash header usage inconsistent with upload mode." });

                case UploadMode.Hashed:
                    return await this.HandleHashedChunkUpload(request, existingContent, newChunkName,
                                                              existingMetadata, hashHeader!, operationStatus, accessTicket, log);

                case UploadMode.Legacy:
                    return await this.HandleLegacyChunkUpload(request, existingContent, newChunkName,
                                                              existingMetadata, operationStatus, accessTicket, log);
            }

            throw new InvalidOperationException("Unreachable.");
```

- [ ] **Step 3: Implement `HandleHashedChunkUpload`**

```csharp
        private async Task<HttpResponseData> HandleHashedChunkUpload(
            HttpRequestData request,
            ContentMetadata existingContent,
            string newChunkName,
            ObjectMetadata existingMetadata,
            string headerHash,
            string operationStatus,
            string accessTicket,
            ILogger log)
        {
            var running = existingContent.RunningHashState is { Length: > 0 }
                ? SerializableSha256.Restore(existingContent.RunningHashState)
                : new SerializableSha256();

            using var chunkHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var tee = new HashingReadStream(request.Body, chunkHasher, running);

            await this.blobHelper.EncryptAndUploadBlobAsync(newChunkName, tee);
            long dataLength;
            try
            {
                dataLength = tee.Length; // may throw — tee.Length throws; fall back to stream.Position if available
            }
            catch
            {
                dataLength = -1;
            }

            var serverHash = Convert.ToHexString(chunkHasher.GetHashAndReset()).ToLowerInvariant();
            if (!serverHash.Equals(headerHash, StringComparison.OrdinalIgnoreCase))
            {
                await this.blobHelper.DeleteBlobIfExistsAsync(newChunkName);
                log.LogWarning("Chunk hash mismatch on {ChunkName}: client claimed {Claimed}, server computed {Computed}",
                    newChunkName, headerHash, serverHash);
                return await ActionResults.CreateResponseAsync(
                    request, HttpStatusCode.UnprocessableEntity,
                    new BaseResponseObject<ChunkHashMismatch>
                    {
                        Status = "chunk_hash_mismatch",
                        Error = "Received chunk bytes do not match client-asserted hash.",
                        Result = new ChunkHashMismatch { Expected = headerHash, Actual = serverHash },
                    });
            }

            existingContent.Chunks.Add(new ChunkMetadata
            {
                ChunkName = newChunkName,
                Hash = serverHash,
                Length = dataLength >= 0 ? dataLength : 0,
            });
            existingContent.RunningHashState = running.SaveState();

            // Hashed-mode: access ticket kept alive; ContentStatus.Ready is flipped at commit, not here.
            existingContent.AccessTicketSetAt = DateTimeProvider.UtcNow;
            existingMetadata.LastAccessedAt = DateTimeProvider.UtcNow;
            await this.dbContext.SaveChangesAsync().ConfigureAwait(false);

            return await ActionResults.CreateResponseAsync(
                request, HttpStatusCode.OK,
                new BaseResponseObject<ChunkCreationOutput>
                {
                    Status = "ok",
                    Result = new ChunkCreationOutput
                    {
                        ChunkName = newChunkName,
                        Hash = serverHash,
                        Length = dataLength >= 0 ? dataLength : 0,
                        AccessTicket = accessTicket,
                    },
                });
        }
```

- [ ] **Step 4: Implement `HandleLegacyChunkUpload`**

Extract the existing chunk-upload body (minus mode-inference) verbatim into a private method with the same parameters.

- [ ] **Step 5: Add `ChunkHashMismatch` DTO**

New file: `SafeExchange.Core/Model/Dto/Output/ChunkHashMismatch.cs`:

```csharp
namespace SafeExchange.Core.Model.Dto.Output;

public class ChunkHashMismatch
{
    public string Expected { get; set; } = string.Empty;

    public string Actual { get; set; } = string.Empty;
}
```

- [ ] **Step 6: Ensure `IBlobHelper.DeleteBlobIfExistsAsync` exists**

Inspect `IBlobHelper`. If the method is missing, add it:

```csharp
Task DeleteBlobIfExistsAsync(string blobName);
```

Implement in the concrete `BlobHelper` using the existing Azure SDK pattern (`await blobClient.DeleteIfExistsAsync();`).

- [ ] **Step 7: Build + run SecretStream tests**

```bash
dotnet build SafeExchange.Core/SafeExchange.Core.csproj -c Release --nologo -v minimal
dotnet test SafeExchange.Tests/SafeExchange.Tests.csproj --filter "FullyQualifiedName~SecretStreamTests" --nologo
```

Expected: all pass.

- [ ] **Step 8: Commit**

```bash
git add SafeExchange.Core/Functions/SafeExchangeSecretStream.cs SafeExchange.Core/Model/Dto/Output/ChunkHashMismatch.cs SafeExchange.Core/Purger/BlobHelper.cs SafeExchange.Core/Purger/IBlobHelper.cs
git commit -m "feat(integrity): hashed-mode chunk upload with per-chunk verify + running state"
```

---

## Phase 7: Backend commit endpoint

### Task 7.1: Tests for `SafeExchangeContentCommit`

**Files:**
- Create: `SafeExchange.Tests/Tests/ContentCommitTests.cs`

- [ ] **Step 1: Write the failing tests**

Use the patterns established by existing function tests (e.g., `SecretStreamTests.cs`). Tests to write:

- `Commit_HashMatches_ReturnsOk_AndFlipsReady`
- `Commit_HashMismatch_Returns422_AndPreservesState`
- `Commit_NoRunningState_Returns422_NoUploadState`
- `Commit_LegacyContent_Returns422_NoUploadState`
- `Commit_BadHashFormat_Returns400`
- `Commit_MissingAccessTicket_ReturnsUnauthorized`
- `Commit_SecondCommit_Returns422_StateAlreadyCleared`

- [ ] **Step 2: Run → expect build failure (class doesn't exist)**

```bash
dotnet test SafeExchange.Tests/SafeExchange.Tests.csproj --filter "FullyQualifiedName~ContentCommitTests" --nologo
```

- [ ] **Step 3: Commit failing tests**

```bash
git add SafeExchange.Tests/Tests/ContentCommitTests.cs
git commit -m "test(integrity): spec commit endpoint behaviour"
```

### Task 7.2: Implement `SafeExchangeContentCommit`

**Files:**
- Create: `SafeExchange.Core/Functions/SafeExchangeContentCommit.cs`

- [ ] **Step 1: Implement the function class**

Structure:

```csharp
namespace SafeExchange.Core.Functions;

using System;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SafeExchange.Core.Configuration;
using SafeExchange.Core.Crypto;
using SafeExchange.Core.Filters;
using SafeExchange.Core.Model;
using SafeExchange.Core.Model.Dto.Output;
using SafeExchange.Core.Permissions;
using SafeExchange.Core.Purger;

public class SafeExchangeContentCommit
{
    public const string PayloadProperty = "hash";

    private static readonly Regex HexRegex = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    private readonly SafeExchangeDbContext dbContext;
    private readonly ITokenHelper tokenHelper;
    private readonly GlobalFilters globalFilters;
    private readonly IPurger purger;
    private readonly IPermissionsManager permissionsManager;

    public SafeExchangeContentCommit(IConfiguration configuration, SafeExchangeDbContext dbContext,
        ITokenHelper tokenHelper, GlobalFilters globalFilters, IPurger purger, IPermissionsManager permissionsManager)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.tokenHelper = tokenHelper ?? throw new ArgumentNullException(nameof(tokenHelper));
        this.globalFilters = globalFilters ?? throw new ArgumentNullException(nameof(globalFilters));
        this.purger = purger ?? throw new ArgumentNullException(nameof(purger));
        this.permissionsManager = permissionsManager ?? throw new ArgumentNullException(nameof(permissionsManager));
    }

    public async Task<HttpResponseData> Run(HttpRequestData request, string secretId, string contentId,
        ClaimsPrincipal principal, ILogger log)
    {
        var (shouldReturn, filterResult) = await this.globalFilters.GetFilterResultAsync(request, principal, this.dbContext);
        if (shouldReturn)
        {
            return filterResult ?? request.CreateResponse(HttpStatusCode.NoContent);
        }

        (SubjectType subjectType, string subjectId) = await SubjectHelper.GetSubjectInfoAsync(this.tokenHelper, principal, this.dbContext);

        log.LogInformation($"{nameof(SafeExchangeContentCommit)} triggered for '{secretId}' ({contentId}) by {subjectType} {subjectId}.");
        await this.purger.PurgeIfNeededAsync(secretId, this.dbContext);

        return await ActionResults.TryCatchAsync(request, async () =>
        {
            var existingMetadata = await this.dbContext.Objects.FindAsync(secretId);
            if (existingMetadata is null)
            {
                return await ActionResults.CreateResponseAsync(request, HttpStatusCode.NotFound,
                    new BaseResponseObject<object> { Status = "not_found", Error = $"Secret '{secretId}' does not exist." });
            }

            if (!existingMetadata.KeepInStorage)
            {
                return await ActionResults.CreateResponseAsync(request, HttpStatusCode.UnprocessableEntity,
                    new BaseResponseObject<object> { Status = "unprocessable", Error = "Cannot commit data for previous-version secrets." });
            }

            if (!(await this.permissionsManager.IsAuthorizedAsync(subjectType, subjectId, secretId, PermissionType.Write)))
            {
                return await ActionResults.CreateResponseAsync(request, HttpStatusCode.Forbidden,
                    ActionResults.InsufficientPermissions(PermissionType.Write, secretId, string.Empty));
            }

            var existingContent = existingMetadata.Content.FirstOrDefault(c => c.ContentName.Equals(contentId));
            if (existingContent is null)
            {
                return await ActionResults.CreateResponseAsync(request, HttpStatusCode.NotFound,
                    new BaseResponseObject<object> { Status = "not_found", Error = $"Content '{contentId}' does not exist." });
            }

            if (existingContent.IsMain)
            {
                return await ActionResults.CreateResponseAsync(request, HttpStatusCode.UnprocessableEntity,
                    new BaseResponseObject<object> { Status = "unprocessable", Error = "Main content does not support explicit commit." });
            }

            var ticketHeader = request.Headers.TryGetValues(SafeExchangeSecretStream.AccessTicketHeaderName, out var tickets)
                ? tickets.FirstOrDefault() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrEmpty(ticketHeader) || !ticketHeader.Equals(existingContent.AccessTicket))
            {
                return await ActionResults.CreateResponseAsync(request, HttpStatusCode.Unauthorized,
                    new BaseResponseObject<object> { Status = "unauthorized", Error = "Access ticket missing or invalid." });
            }

            if (existingContent.RunningHashState is null || existingContent.RunningHashState.Length == 0)
            {
                return await ActionResults.CreateResponseAsync(request, HttpStatusCode.UnprocessableEntity,
                    new BaseResponseObject<object> { Status = "no_upload_state", Error = "No hashed-mode upload in progress for this content." });
            }

            CommitRequest? payload;
            try
            {
                payload = await JsonSerializer.DeserializeAsync<CommitRequest>(request.Body,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException)
            {
                return await ActionResults.CreateResponseAsync(request, HttpStatusCode.BadRequest,
                    new BaseResponseObject<object> { Status = "bad_request", Error = "Body is not valid JSON." });
            }

            var clientHash = payload?.Hash?.ToLowerInvariant() ?? string.Empty;
            if (!HexRegex.IsMatch(clientHash))
            {
                return await ActionResults.CreateResponseAsync(request, HttpStatusCode.BadRequest,
                    new BaseResponseObject<object> { Status = "bad_request", Error = "hash must be 64 hex characters." });
            }

            var running = SerializableSha256.Restore(existingContent.RunningHashState);
            var serverHash = Convert.ToHexString(running.Finish()).ToLowerInvariant();

            if (!serverHash.Equals(clientHash))
            {
                log.LogWarning("Commit hash mismatch for '{ContentId}': client {Client}, server {Server}", contentId, clientHash, serverHash);
                return await ActionResults.CreateResponseAsync(request, HttpStatusCode.UnprocessableEntity,
                    new BaseResponseObject<ChunkHashMismatch>
                    {
                        Status = "hash_mismatch",
                        Error = "Client-asserted whole-content hash does not match server computation.",
                        Result = new ChunkHashMismatch { Expected = clientHash, Actual = serverHash },
                    });
            }

            existingContent.Hash = serverHash;
            existingContent.Status = ContentStatus.Ready;
            existingContent.AccessTicket = string.Empty;
            existingContent.AccessTicketSetAt = DateTime.MinValue;
            existingContent.RunningHashState = null;
            existingMetadata.LastAccessedAt = DateTimeProvider.UtcNow;
            await this.dbContext.SaveChangesAsync();

            return await ActionResults.CreateResponseAsync(request, HttpStatusCode.OK,
                new BaseResponseObject<ContentCommitOutput>
                {
                    Status = "ok",
                    Result = new ContentCommitOutput { ContentName = contentId, Hash = serverHash },
                });
        }, nameof(SafeExchangeContentCommit), log);
    }

    private sealed class CommitRequest
    {
        public string? Hash { get; set; }
    }
}
```

- [ ] **Step 2: Create `ContentCommitOutput` DTO**

```csharp
namespace SafeExchange.Core.Model.Dto.Output;

public class ContentCommitOutput
{
    public string ContentName { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;
}
```

- [ ] **Step 3: Register in `SafeSecret` HTTP trigger**

Add:

```csharp
        private SafeExchangeContentCommit contentCommitHandler;
```

In the constructor:

```csharp
            this.contentCommitHandler = new SafeExchangeContentCommit(configuration, dbContext, tokenHelper, globalFilters, purger, permissionsManager);
```

New trigger:

```csharp
        [Function("SafeExchange-SecretContentCommit")]
        public async Task<HttpResponseData> RunCommitContent(
            [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = $"{Version}/secret/{{secretId}}/content/{{contentId}}/commit")]
            HttpRequestData request, ClaimsPrincipal principal, string secretId, string contentId)
        {
            return await this.contentCommitHandler.Run(request, secretId, contentId, principal, this.log);
        }
```

- [ ] **Step 4: Run tests**

```bash
dotnet test SafeExchange.Tests/SafeExchange.Tests.csproj --filter "FullyQualifiedName~ContentCommitTests" --nologo
```

Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add SafeExchange.Core/Functions/SafeExchangeContentCommit.cs SafeExchange.Core/Model/Dto/Output/ContentCommitOutput.cs SafeExchange.Functions/Functions/SafeSecret.cs
git commit -m "feat(integrity): add PATCH /content/commit endpoint with whole-hash verify"
```

---

## Phase 8: Backend purger + integration

### Task 8.1: Ensure purger clears `RunningHashState`

**Files:**
- Modify: `SafeExchange.Core/Purger/PurgeManager.cs`

- [ ] **Step 1: Grep for existing chunk-clear logic**

```bash
grep -n "Chunks.Clear\|AccessTicket = string.Empty" SafeExchange.Core/Purger/PurgeManager.cs SafeExchange.Core/Functions/SafeExchangeSecretStream.cs
```

- [ ] **Step 2: Ensure every site that clears chunks also sets `RunningHashState = null`**

In `PurgeManager`, wherever `content.Chunks.Clear()` runs, add `content.RunningHashState = null;`. Same in `TryGetAccessTicketAsync` in `SafeExchangeSecretStream.cs`.

- [ ] **Step 3: Add a regression test** in `SecretStreamTests.cs`:

`AccessTicketExpires_ClearsRunningHashState`:
- Seed content with `RunningHashState = new byte[] { 1, 2 }` and `AccessTicketSetAt = long ago`.
- Call the upload handler (it triggers ticket expiry via `TryGetAccessTicketAsync`).
- Assert `content.RunningHashState == null` after.

- [ ] **Step 4: Build + test**

```bash
dotnet test SafeExchange.Tests/SafeExchange.Tests.csproj --nologo
```

- [ ] **Step 5: Commit**

```bash
git add SafeExchange.Core/Purger/PurgeManager.cs SafeExchange.Core/Functions/SafeExchangeSecretStream.cs SafeExchange.Tests/Tests/SecretStreamTests.cs
git commit -m "fix(integrity): purge RunningHashState alongside chunks on expiry"
```

### Task 8.2: Deploy backend to staging

**Files:** none — deploy only.

- [ ] **Step 1: Confirm flag defaults**

Check the backend staging Key Vault / App Configuration for `Features:AllowLegacyAttachmentUploads` — must be `true` (or unset, binding to the class default which is also `true`). Do **not** flip `IgnoreChunkHashHeader`.

- [ ] **Step 2: Deploy**

```bash
cd /c/Users/yurio/Documents/github/safeexchange
ls deployment/
```

Follow the existing backend deploy runbook (there's a `deployment/` folder with scripts — use the same pattern that the earlier compaction shows was used for previous backend deploys).

- [ ] **Step 3: Verify staging health**

```bash
curl -sI https://safeexchange-staging.azurewebsites.net/api/ping 2>&1 | head -5
```

(Or whatever the ping/health endpoint is.) Expected: HTTP 200.

- [ ] **Step 4: Spot-check old client still works**

Use the existing staging client (old client bundle, no integrity header). Sign in, upload a small attachment, verify it still lands. Legacy-mode path exercised.

- [ ] **Step 5: No commit** — deploy only. Note the deploy timestamp in the running notes.

---

## Phase 9: Client DTOs + ApiClient

### Task 9.1: Client-side `ContentMetadata.Hash`

**Files:**
- Modify: `SafeExchange.Client.Common/Model/ContentMetadata.cs`
- Modify: `SafeExchange.Client.Common/Model/ContentMetadataOutput.cs` (if separate from the above — grep first)

- [ ] **Step 1: Add the property**

```csharp
        public string? Hash { get; set; }
```

Mirror it on `ContentMetadataOutput` (the output DTO) with the same nullability.

- [ ] **Step 2: Propagate via existing copy constructors / mappers** (e.g. `new ContentMetadata(source)`).

- [ ] **Step 3: Build**

```bash
cd /c/Users/yurio/Documents/github/safeexchange.blazorpwa
dotnet build SafeExchange.Client.Common/SafeExchange.Client.Common.csproj -c Release --nologo -v minimal
```

- [ ] **Step 4: Commit**

```bash
git add SafeExchange.Client.Common/Model/ContentMetadata.cs SafeExchange.Client.Common/Model/ContentMetadataOutput.cs
git commit -m "feat(integrity): client ContentMetadata carries whole-content Hash"
```

### Task 9.2: `ApiClient` — wire `chunkHash` header + `CommitContentAsync`

**Files:**
- Modify: `SafeExchange.Client.Common/ApiClient/ApiClient.cs`
- Modify: `SafeExchange.Client.Common/Constants.cs` (if a client-side constants file exists; otherwise add one)

- [ ] **Step 1: Add `ChunkHashHeaderName` constant**

```csharp
        public const string ChunkHashHeaderName = "X-SafeExchange-Chunk-Hash";
```

- [ ] **Step 2: Modify `PutSecretDataStreamAsync` signature**

Add a parameter `string? chunkHash = null` at the end (optional for backward compat with any caller we haven't migrated yet). When non-null, add the header:

```csharp
            if (!string.IsNullOrEmpty(chunkHash))
            {
                httpRequestMessage.Headers.Add(ChunkHashHeaderName, chunkHash);
            }
```

- [ ] **Step 3: Add `CommitContentAsync`**

Right after `PutSecretDataStreamAsync`:

```csharp
        public async Task<BaseResponseObject<ContentCommitOutput>> CommitContentAsync(
            string secretId, string contentId, string contentHash, string accessTicket)
            => await this.ProcessResponseAsync<ContentCommitOutput>(async () =>
        {
            var httpRequestMessage = new HttpRequestMessage(
                new HttpMethod("PATCH"),
                $"{ApiVersion}/secret/{secretId}/content/{contentId}/commit")
            {
                Content = JsonContent.Create(new { hash = contentHash }, mediaType: null),
            };
            if (!string.IsNullOrEmpty(accessTicket))
            {
                httpRequestMessage.Headers.Add("X-SafeExchange-Ticket", accessTicket);
            }

            return await client.SendAsync(httpRequestMessage);
        });
```

- [ ] **Step 4: Create `ContentCommitOutput` client DTO** in `SafeExchange.Client.Common/Model/ContentCommitOutput.cs`:

```csharp
namespace SafeExchange.Client.Common.Model;

public class ContentCommitOutput
{
    public string ContentName { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;
}
```

- [ ] **Step 5: Build**

```bash
dotnet build SafeExchange.Client.Common/SafeExchange.Client.Common.csproj -c Release --nologo -v minimal
```

- [ ] **Step 6: Commit**

```bash
git add SafeExchange.Client.Common/ApiClient/ApiClient.cs SafeExchange.Client.Common/Model/ContentCommitOutput.cs
git commit -m "feat(integrity): ApiClient sends chunk hash + CommitContentAsync"
```

### Task 9.3: Revise `UploadAttachmentsAsync` with hashed flow

**Files:**
- Modify: `SafeExchange.Client.Common/ApiClient/ApiClient.cs`

- [ ] **Step 1: Replace the chunk loop with hashing version**

The new loop, inside the existing `try` block of `UploadAttachmentsAsync`:

```csharp
                    var dataStream = attachment.SourceFile.OpenReadStream(Constants.MaxAttachmentDataLength);
                    var chunkLengths = GetChunkLengths(attachment.SourceFile.Size, Constants.MaxChunkDataLength);
                    var accessTicket = string.Empty;
                    using var fileHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                    for (int chunkIndex = 0; chunkIndex < chunkLengths.Count; chunkIndex++)
                    {
                        var isInterim = chunkIndex < (chunkLengths.Count - 1);
                        var size = chunkLengths[chunkIndex];
                        var buffer = new byte[size];
                        var readTotal = 0;
                        while (readTotal < size)
                        {
                            var got = await dataStream.ReadAsync(buffer.AsMemory(readTotal, size - readTotal));
                            if (got == 0)
                            {
                                break;
                            }
                            readTotal += got;
                        }

                        var chunkHash = Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0, readTotal))).ToLowerInvariant();
                        fileHasher.AppendData(buffer, 0, readTotal);

                        using var chunkMemory = new MemoryStream(buffer, 0, readTotal, writable: false);
                        var chunkResponse = await this.PutSecretDataStreamAsync(
                            secretId, content.ContentName, chunkMemory, isInterim, readTotal, accessTicket, chunkHash);

                        if (!"ok".Equals(chunkResponse.Status) || chunkResponse.Result is null)
                        {
                            attachment.Status = UploadStatus.Error;
                            attachment.Error = chunkResponse.Status == "chunk_hash_mismatch"
                                ? $"Chunk {chunkIndex + 1}/{chunkLengths.Count} hash mismatch — upload aborted."
                                : $"'Attachment {attachment.SourceFile.Name}' upload failed.";
                            break;
                        }

                        accessTicket = chunkResponse.Result.AccessTicket;
                        attachment.ProgressPercents += 100.0f * ((float)readTotal / attachment.SourceFile.Size);
                    }

                    if (attachment.Status == UploadStatus.Error)
                    {
                        continue;
                    }

                    var contentHash = Convert.ToHexString(fileHasher.GetHashAndReset()).ToLowerInvariant();
                    var commitResponse = await this.CommitContentAsync(
                        secretId, content.ContentName, contentHash, accessTicket);

                    if (!"ok".Equals(commitResponse.Status))
                    {
                        attachment.Status = UploadStatus.Error;
                        attachment.Error = commitResponse.Status == "hash_mismatch"
                            ? "Whole-content hash mismatch on commit — please retry the upload."
                            : $"Commit failed: {commitResponse.Error}";
                        continue;
                    }

                    attachment.Status = UploadStatus.Success;
                    attachment.ProgressPercents = 100.0f;
```

- [ ] **Step 2: Add the `using System.Security.Cryptography;` import** if missing.

- [ ] **Step 3: Build**

```bash
dotnet build SafeExchange.Client.Web.Components/SafeExchange.Client.Web.Components.csproj -c Release --nologo -v minimal
```

Expected: `0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add SafeExchange.Client.Common/ApiClient/ApiClient.cs
git commit -m "feat(integrity): UploadAttachmentsAsync hashes chunks + calls commit"
```

---

## Phase 10: Client JS helpers for verified download

### Task 10.1: `saexDownload.js` helper

**Files:**
- Create: `SafeExchange.Client.Web.Components/wwwroot/js/saexDownload.js`

- [ ] **Step 1: Write the helper**

```javascript
// saexDownload.js
//
// Verified download helper. Two back-ends:
//   1) File System Access API (Chromium/Safari/Edge) — streams bytes to user-chosen
//      file location, aborts cleanly on integrity failure.
//   2) In-memory Blob (Firefox / fallback) — accumulates bytes in memory, only
//      reveals via <a download> after verification succeeds.
//
// Surface exposed to C#:
//   window.saexDownload = {
//     startVerifiedSave(fileName, contentType) -> Promise<handle>
//     writeBlock(handle, uint8Array)           -> Promise<void>
//     abort(handle)                             -> Promise<void>
//     finalize(handle)                          -> Promise<void>  (reveals the save)
//   };

(function () {
    const handles = new Map();
    let nextId = 1;

    async function startVerifiedSave(fileName, contentType) {
        const id = String(nextId++);
        if (typeof window.showSaveFilePicker === "function") {
            try {
                const fileHandle = await window.showSaveFilePicker({
                    suggestedName: fileName,
                    types: contentType ? [{ description: fileName, accept: { [contentType]: ["." + (fileName.split(".").pop() || "bin")] } }] : undefined,
                });
                const writable = await fileHandle.createWritable();
                handles.set(id, { kind: "fsa", writable, fileHandle, fileName, contentType });
                return id;
            } catch (err) {
                if (err && err.name === "AbortError") {
                    throw err;
                }
                // Any other FSA failure → fall back to blob accumulator.
            }
        }
        handles.set(id, { kind: "blob", buffers: [], fileName, contentType });
        return id;
    }

    async function writeBlock(id, bytes) {
        const h = handles.get(id);
        if (!h) {
            throw new Error("Unknown download handle.");
        }
        if (h.kind === "fsa") {
            await h.writable.write(bytes);
        } else {
            h.buffers.push(bytes.slice()); // copy — Blazor may reuse the underlying buffer
        }
    }

    async function abort(id) {
        const h = handles.get(id);
        if (!h) {
            return;
        }
        if (h.kind === "fsa") {
            try { await h.writable.abort(); } catch { /* swallow */ }
        }
        // blob: just drop buffers — no file ever created.
        handles.delete(id);
    }

    async function finalize(id) {
        const h = handles.get(id);
        if (!h) {
            throw new Error("Unknown download handle.");
        }
        if (h.kind === "fsa") {
            try { await h.writable.close(); } finally { handles.delete(id); }
        } else {
            const blob = new Blob(h.buffers, { type: h.contentType || "application/octet-stream" });
            const url = URL.createObjectURL(blob);
            try {
                const a = document.createElement("a");
                a.href = url;
                a.download = h.fileName || "download";
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
            } finally {
                URL.revokeObjectURL(url);
                handles.delete(id);
            }
        }
    }

    window.saexDownload = { startVerifiedSave, writeBlock, abort, finalize };
})();
```

- [ ] **Step 2: Add script reference to `wwwroot/index.html`**

Near the other script tags (before `blazor.webassembly.js`):

```html
<script src="_content/SafeExchange.Client.Web.Components/js/saexDownload.js"></script>
```

(Or however the existing wiring includes `tooltipsInitializer.js` — follow the same pattern. If the file is referenced via `<link href>` style in `_content`, mirror that.)

- [ ] **Step 3: Commit**

```bash
git add SafeExchange.Client.Web.Components/wwwroot/js/saexDownload.js SafeExchange.PWA/wwwroot/index.html
git commit -m "feat(integrity): add saexDownload.js for verified streaming download"
```

### Task 10.2: `VerifiedDownloadHelper` (C# wrapper)

**Files:**
- Create: `SafeExchange.Client.Web.Components/Classes/Helpers/VerifiedDownloadHelper.cs`

- [ ] **Step 1: Write the helper**

```csharp
namespace SafeExchange.Client.Web.Components.Helpers;

using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using SafeExchange.Client.Common;
using SafeExchange.Client.Common.ApiClient;
using SafeExchange.Client.Common.Model;

public sealed class VerifiedDownloadHelper
{
    private const int ReadBufferSize = 64 * 1024;

    private readonly IJSRuntime jsRuntime;
    private readonly ApiClient apiClient;

    public VerifiedDownloadHelper(IJSRuntime jsRuntime, ApiClient apiClient)
    {
        this.jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public async Task<VerifiedDownloadResult> DownloadAsync(
        string secretId,
        ContentMetadata content,
        IProgress<VerifiedDownloadProgress>? progress = null)
    {
        var handle = await this.jsRuntime.InvokeAsync<string>(
            "saexDownload.startVerifiedSave", content.FileName, content.ContentType);

        using var fileHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long totalRead = 0;
        long totalSize = 0;
        foreach (var chunk in content.Chunks)
        {
            totalSize += chunk.Length;
        }

        try
        {
            for (var chunkIndex = 0; chunkIndex < content.Chunks.Count; chunkIndex++)
            {
                var chunk = content.Chunks[chunkIndex];
                using var chunkHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var stream = await this.apiClient.GetSecretDataStreamAsync(secretId, content.ContentName, chunk.ChunkName);
                if (!"ok".Equals(stream.Status) || stream.Result is null)
                {
                    await this.jsRuntime.InvokeVoidAsync("saexDownload.abort", handle);
                    return VerifiedDownloadResult.Failed(chunkIndex, content.Chunks.Count, "Chunk fetch failed.");
                }

                var buffer = new byte[ReadBufferSize];
                int read;
                while ((read = await stream.Result.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    var segment = new ArraySegment<byte>(buffer, 0, read);
                    chunkHasher.AppendData(buffer, 0, read);
                    fileHasher.AppendData(buffer, 0, read);
                    await this.jsRuntime.InvokeVoidAsync("saexDownload.writeBlock", handle, segment);
                    totalRead += read;
                    progress?.Report(new VerifiedDownloadProgress(totalRead, totalSize, chunkIndex, content.Chunks.Count));
                }

                var chunkHex = Convert.ToHexString(chunkHasher.GetHashAndReset()).ToLowerInvariant();
                if (!string.IsNullOrEmpty(chunk.Hash) && !chunkHex.Equals(chunk.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    await this.jsRuntime.InvokeVoidAsync("saexDownload.abort", handle);
                    return VerifiedDownloadResult.Failed(chunkIndex, content.Chunks.Count, $"Chunk {chunkIndex + 1} hash mismatch.");
                }
            }

            var fileHex = Convert.ToHexString(fileHasher.GetHashAndReset()).ToLowerInvariant();
            if (!string.IsNullOrEmpty(content.Hash) && !fileHex.Equals(content.Hash, StringComparison.OrdinalIgnoreCase))
            {
                await this.jsRuntime.InvokeVoidAsync("saexDownload.abort", handle);
                return VerifiedDownloadResult.Failed(-1, content.Chunks.Count, "Whole-content hash mismatch.");
            }

            await this.jsRuntime.InvokeVoidAsync("saexDownload.finalize", handle);
            return VerifiedDownloadResult.Success(fileHex);
        }
        catch
        {
            await this.jsRuntime.InvokeVoidAsync("saexDownload.abort", handle);
            throw;
        }
    }
}

public readonly record struct VerifiedDownloadProgress(long BytesWritten, long TotalBytes, int CurrentChunk, int TotalChunks);

public sealed class VerifiedDownloadResult
{
    private VerifiedDownloadResult(bool ok, string? hash, int? failedAtChunk, int chunkCount, string? error)
    {
        this.IsSuccess = ok;
        this.ComputedHash = hash;
        this.FailedAtChunk = failedAtChunk;
        this.ChunkCount = chunkCount;
        this.Error = error;
    }

    public bool IsSuccess { get; }

    public string? ComputedHash { get; }

    public int? FailedAtChunk { get; }

    public int ChunkCount { get; }

    public string? Error { get; }

    public static VerifiedDownloadResult Success(string hash)
        => new(true, hash, null, 0, null);

    public static VerifiedDownloadResult Failed(int atChunk, int totalChunks, string error)
        => new(false, null, atChunk, totalChunks, error);
}
```

- [ ] **Step 2: Register in DI** in `ServicesHelper.cs`:

Under the other `AddScoped` calls:

```csharp
            builder.Services.AddScoped<VerifiedDownloadHelper>();
```

- [ ] **Step 3: Build**

```bash
dotnet build SafeExchange.Client.Web.Components/SafeExchange.Client.Web.Components.csproj -c Release --nologo -v minimal
```

- [ ] **Step 4: Commit**

```bash
git add SafeExchange.Client.Web.Components/Classes/Helpers/VerifiedDownloadHelper.cs SafeExchange.Client.Web.Components/ServicesHelper.cs
git commit -m "feat(integrity): VerifiedDownloadHelper wraps streaming + verify flow"
```

---

## Phase 11: Client UI

### Task 11.1: Locate the attachment row component

**Files (read):**
- `SafeExchange.Client.Web.Components/Pages/ViewData.razor`
- any partial component rendering attachments list (search for `attachment`, `Attachments`, `MainData`, `SecretContentStream`)

- [ ] **Step 1: Grep for the existing attachment UI**

```bash
grep -ln -i "attachment\|secretcontent" SafeExchange.Client.Web.Components/Pages/ViewData.razor SafeExchange.Client.Web.Components/Shared/*.razor
```

- [ ] **Step 2: Note the row template's current structure** — the edits below assume a per-attachment `<tr>` or `<div>` in `ViewData.razor`. Adjust path accordingly.

### Task 11.2: Hash cell + badges in the attachment row

**Files:**
- Modify: `SafeExchange.Client.Web.Components/Pages/ViewData.razor`

- [ ] **Step 1: Add helper method inside `@code { }`**

```csharp
    private static string TruncateHash(string? hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            return string.Empty;
        }
        return hash.Length > 8 ? hash.Substring(0, 8) + "…" : hash;
    }

    private AttachmentBadgeKind ResolveBadge(ContentMetadata content, AttachmentVerifyResult? lastResult)
    {
        if (string.IsNullOrEmpty(content.Hash))
        {
            return AttachmentBadgeKind.Legacy;
        }
        return lastResult switch
        {
            AttachmentVerifyResult.Success => AttachmentBadgeKind.Verified,
            AttachmentVerifyResult.Failure => AttachmentBadgeKind.Failed,
            _ => AttachmentBadgeKind.None,
        };
    }

    private enum AttachmentBadgeKind { None, Verified, Legacy, Failed }

    private enum AttachmentVerifyResult { Success, Failure }
```

- [ ] **Step 2: Render the hash cell + badge next to the attachment filename**

Inside the attachment `<tr>` / row template, alongside the filename and size, add:

```razor
        @if (!string.IsNullOrEmpty(attachment.Hash))
        {
            <code data-bs-toggle="tooltip" data-bs-placement="top" title="@attachment.Hash" class="me-1">@TruncateHash(attachment.Hash)</code>
            <button type="button" class="btn btn-sm btn-link p-0" @onclick="@(() => this.Clipboard.WriteTextAsync(attachment.Hash))" data-bs-toggle="tooltip" data-bs-placement="top" title="Copy hash">
                <i class="bi bi-clipboard"></i>
            </button>
        }
        @switch (ResolveBadge(attachment, this.lastVerifyResults.GetValueOrDefault(attachment.ContentName)))
        {
            case AttachmentBadgeKind.Verified:
                <i class="bi bi-shield-fill-check text-success ms-2" data-bs-toggle="tooltip" title="Integrity verified on download."></i>
                break;
            case AttachmentBadgeKind.Legacy:
                <i class="bi bi-question-diamond text-secondary ms-2" data-bs-toggle="tooltip" title="Uploaded before the integrity feature — no reference hash is stored. Re-upload this attachment to enable verification."></i>
                break;
            case AttachmentBadgeKind.Failed:
                <i class="bi bi-exclamation-diamond text-danger ms-2" data-bs-toggle="tooltip" title="Last download failed the integrity check. The file on disk may be partial or corrupt."></i>
                break;
        }
```

- [ ] **Step 3: Declare the session-scoped dictionary**

```csharp
    private readonly Dictionary<string, AttachmentVerifyResult> lastVerifyResults = new();
```

- [ ] **Step 4: Inject `ClipboardService`** via `@inject ClipboardService Clipboard` at the top of the page if not already injected.

- [ ] **Step 5: Build + visually smoke-test with `dotnet run`** on the PWA project to ensure it renders (no runtime error in console).

- [ ] **Step 6: Commit**

```bash
git add SafeExchange.Client.Web.Components/Pages/ViewData.razor
git commit -m "feat(integrity): attachment row shows truncated hash + status badge"
```

### Task 11.3: Wire the download button to `VerifiedDownloadHelper`

**Files:**
- Modify: `SafeExchange.Client.Web.Components/Pages/ViewData.razor` (`DownloadToFileStreamAsync` replacement)

- [ ] **Step 1: Inject the helper**

`@inject VerifiedDownloadHelper VerifiedDownload`

- [ ] **Step 2: Replace the existing download trigger**

Find `DownloadToFileStreamAsync`-call site. Replace the body with:

```csharp
    private async Task DownloadAttachmentAsync(ContentMetadata content)
    {
        if (string.IsNullOrEmpty(content.Hash))
        {
            // Legacy fallback — plain old download path, no verify.
            await this.LegacyDownloadAsync(content);
            return;
        }

        try
        {
            var result = await this.VerifiedDownload.DownloadAsync(
                this.secretId,
                content,
                new Progress<VerifiedDownloadProgress>(p => { /* update progress UI */ }));

            if (result.IsSuccess)
            {
                this.lastVerifyResults[content.ContentName] = AttachmentVerifyResult.Success;
                // optional: telemetry
            }
            else
            {
                this.lastVerifyResults[content.ContentName] = AttachmentVerifyResult.Failure;
                this.integrityFailure = (content, result);
            }
        }
        catch (Exception ex)
        {
            this.lastVerifyResults[content.ContentName] = AttachmentVerifyResult.Failure;
            this.integrityFailure = (content, VerifiedDownloadResult.Failed(-1, content.Chunks.Count, ex.Message));
        }

        this.StateHasChanged();
    }
```

Declare `private (ContentMetadata content, VerifiedDownloadResult result)? integrityFailure;` in `@code`.

Preserve the current `LegacyDownloadAsync` method (which is the original `DownloadToFileStreamAsync` code — rename it).

- [ ] **Step 3: Add a Bootstrap modal** near the bottom of the page for `integrityFailure` display. Uses the same patterns as existing modals:

```razor
@if (this.integrityFailure.HasValue)
{
    <div class="modal d-block" tabindex="-1" role="dialog" style="background-color: rgba(0,0,0,0.4);">
        <div class="modal-dialog" role="document">
            <div class="modal-content">
                <div class="modal-header bg-danger text-white">
                    <h5 class="modal-title"><i class="bi bi-exclamation-diamond"></i> Integrity check failed</h5>
                </div>
                <div class="modal-body">
                    <p>The download of <strong>@this.integrityFailure.Value.content.FileName</strong> could not be verified against the reference hash stored with this attachment.</p>
                    <p class="text-muted small">The file at the chosen location may be partial or corrupt — please delete it.</p>
                    @if (this.integrityFailure.Value.result.FailedAtChunk.HasValue && this.integrityFailure.Value.result.FailedAtChunk.Value >= 0)
                    {
                        <p class="text-muted small">Failed at chunk @(this.integrityFailure.Value.result.FailedAtChunk.Value + 1)/@this.integrityFailure.Value.result.ChunkCount.</p>
                    }
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" @onclick="@(() => this.integrityFailure = null)">Dismiss</button>
                </div>
            </div>
        </div>
    </div>
}
```

- [ ] **Step 4: Build**

```bash
dotnet build SafeExchange.Client.Web.Components/SafeExchange.Client.Web.Components.csproj -c Release --nologo -v minimal
```

- [ ] **Step 5: Commit**

```bash
git add SafeExchange.Client.Web.Components/Pages/ViewData.razor
git commit -m "feat(integrity): verified-download flow + failure modal"
```

### Task 11.4: "Verify local file…" action

**Files:**
- Modify: `SafeExchange.Client.Web.Components/Pages/ViewData.razor`
- Create: `SafeExchange.Client.Web.Components/Classes/Helpers/LocalFileVerifier.cs`

- [ ] **Step 1: Write the verifier**

```csharp
namespace SafeExchange.Client.Web.Components.Helpers;

using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;

public sealed class LocalFileVerifier
{
    public async Task<string> ComputeHashAsync(IBrowserFile file, long maxBytes = 100L * 1024 * 1024)
    {
        using var stream = file.OpenReadStream(maxBytes);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            hasher.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
    }
}
```

Register in `ServicesHelper`:

```csharp
            builder.Services.AddScoped<LocalFileVerifier>();
```

- [ ] **Step 2: Add the action to the attachment row**

In `ViewData.razor`:

```razor
<div class="dropdown d-inline-block">
    <button type="button" class="btn btn-sm btn-link" data-bs-toggle="dropdown"><i class="bi bi-three-dots-vertical"></i></button>
    <ul class="dropdown-menu">
        <li>
            <button type="button"
                    class="dropdown-item @(string.IsNullOrEmpty(attachment.Hash) ? "disabled" : "")"
                    disabled="@string.IsNullOrEmpty(attachment.Hash)"
                    @onclick="@(() => this.TriggerLocalVerifyAsync(attachment))">
                Verify local file…
            </button>
        </li>
    </ul>
</div>

<InputFile id="@($"verifyInput-{attachment.ContentName}")" style="display:none"
           OnChange="@(e => this.RunLocalVerifyAsync(attachment, e))" />
```

- [ ] **Step 3: Handler methods**

```csharp
    private ContentMetadata? pendingVerifyContent;
    private string? localVerifyMessage;
    private bool localVerifySuccess;

    private async Task TriggerLocalVerifyAsync(ContentMetadata attachment)
    {
        this.pendingVerifyContent = attachment;
        await this.JSRuntime.InvokeVoidAsync("document.getElementById('verifyInput-" + attachment.ContentName + "').click");
    }

    private async Task RunLocalVerifyAsync(ContentMetadata attachment, InputFileChangeEventArgs args)
    {
        var file = args.File;
        var computed = await this.LocalFileVerifier.ComputeHashAsync(file);
        this.localVerifySuccess = string.Equals(computed, attachment.Hash, StringComparison.OrdinalIgnoreCase);
        this.localVerifyMessage = this.localVerifySuccess
            ? $"\u2713 {file.Name} matches this attachment."
            : $"\u2717 {file.Name} does NOT match this attachment. Computed: {computed.Substring(0, 12)}…";
        this.StateHasChanged();
    }
```

- [ ] **Step 4: Render result toast** near the modal:

```razor
@if (this.localVerifyMessage is not null)
{
    <div class="toast-container position-fixed top-0 end-0 p-3" style="z-index: 1080;">
        <div class="toast show @(this.localVerifySuccess ? "bg-success" : "bg-danger") text-white">
            <div class="toast-body d-flex justify-content-between">
                <span>@this.localVerifyMessage</span>
                <button type="button" class="btn-close btn-close-white ms-2" @onclick="@(() => this.localVerifyMessage = null)"></button>
            </div>
        </div>
    </div>
}
```

- [ ] **Step 5: Build**

```bash
dotnet build SafeExchange.Client.Web.Components/SafeExchange.Client.Web.Components.csproj -c Release --nologo -v minimal
```

- [ ] **Step 6: Commit**

```bash
git add SafeExchange.Client.Web.Components/Classes/Helpers/LocalFileVerifier.cs SafeExchange.Client.Web.Components/Pages/ViewData.razor SafeExchange.Client.Web.Components/ServicesHelper.cs
git commit -m "feat(integrity): Verify local file action with match/mismatch toast"
```

---

## Phase 12: Deploy client to staging + Playwright smoke

### Task 12.1: Deploy client to staging

**Files:** none — deploy.

- [ ] **Step 1: Build + run final tests**

```bash
cd /c/Users/yurio/Documents/github/safeexchange.blazorpwa
dotnet build -c Release --nologo -v minimal
```

Expected: `0 Error(s)`.

- [ ] **Step 2: Push the feature branch**

```bash
git push -u origin features/attachments-integrity
```

- [ ] **Step 3: Deploy to staging**

```bash
pwsh -File deployment/deploy-pwa.ps1 -Environment test
```

Expected: deploy completes with AFD cache purge.

### Task 12.2: Playwright E2E — happy path

**Files:**
- Create: `playwright/attachments-integrity.spec.ts` (or the equivalent location — grep the repo for existing Playwright setup first: `grep -rln "playwright" --include=package.json --include=*.config.ts`).

- [ ] **Step 1: Confirm a Playwright harness already exists; if not, add a minimal one**

```bash
find . -maxdepth 3 -name "playwright.config.*" 2>/dev/null
find . -maxdepth 3 -name "package.json" 2>/dev/null
```

If no harness exists: create a minimal `playwright.config.ts` + `package.json` in `playwright/`, install via `npm i -D @playwright/test`, `npx playwright install chromium`.

- [ ] **Step 2: Write the test**

```typescript
import { test, expect } from "@playwright/test";
import * as path from "node:path";
import * as os from "node:os";
import * as fs from "node:fs";
import * as crypto from "node:crypto";

const STAGING = "https://safeexchange-staging-f3atejahehd7c6ga.z01.azurefd.net";
const USER = "test.2.user@spaceoysteroutlook.onmicrosoft.com";
const PASSWORD = "1d4be0d9-703f-4d32-ace8-aa6b9a5d2bee";

test("upload + download + verified integrity", async ({ page }) => {
    // 1. Make a 1 MB random file on disk
    const bytes = crypto.randomBytes(1_048_576);
    const expectedHex = crypto.createHash("sha256").update(bytes).digest("hex");
    const tmpPath = path.join(os.tmpdir(), `saex-upload-${Date.now()}.bin`);
    fs.writeFileSync(tmpPath, bytes);

    // 2. Sign in
    await page.goto(STAGING);
    await page.getByRole("link", { name: /sign in/i }).click();
    await page.getByLabel(/email|username/i).fill(USER);
    await page.getByRole("button", { name: /next|continue/i }).click();
    await page.getByLabel(/password/i).fill(PASSWORD);
    await page.getByRole("button", { name: /sign in|submit/i }).click();

    // 3. Create a new secret with the attachment
    await page.getByRole("button", { name: /new|create/i }).click();
    await page.getByLabel(/name/i).fill(`integrity-${Date.now()}`);
    await page.setInputFiles('input[type="file"]', tmpPath);
    await page.getByRole("button", { name: /save|create/i }).click();

    // 4. Verify the hash badge is green on the attachment row
    await expect(page.locator(".bi-shield-fill-check")).toBeVisible({ timeout: 30_000 });

    // 5. Verify the displayed truncated hash matches the expected hex prefix
    const truncated = expectedHex.slice(0, 8) + "…";
    await expect(page.getByText(truncated)).toBeVisible();

    // Cleanup
    fs.unlinkSync(tmpPath);
});
```

- [ ] **Step 3: Run Playwright against staging**

```bash
cd playwright
npx playwright test attachments-integrity --reporter=list
```

Expected: the test passes. If it fails, inspect screenshots/video and fix the code; re-deploy staging; re-run.

- [ ] **Step 4: Commit**

```bash
git add playwright/
git commit -m "test(integrity): Playwright E2E covers upload + verified download on staging"
```

### Task 12.3: Playwright — local-file verify match + mismatch

- [ ] **Step 1: Extend the spec file** with two more tests:

```typescript
test("verify local file: same bytes → match toast", async ({ page }) => { /* similar flow, click kebab → Verify local file, pick same file, expect green toast */ });
test("verify local file: different bytes → mismatch toast", async ({ page }) => { /* same but different file */ });
```

Specifics left to the implementer based on the exact DOM selectors (run Playwright codegen against staging to harvest selectors).

- [ ] **Step 2: Run + commit**

```bash
npx playwright test attachments-integrity
git add playwright/
git commit -m "test(integrity): local-file verify match + mismatch E2E"
```

---

## Phase 13: Flag flip + writeup

### Task 13.1: Flip `AllowLegacyAttachmentUploads=false` on staging

**Files:** none.

- [ ] **Step 1: Check AI telemetry `AttachmentUploadSucceeded` dimension `mode`**

```bash
az monitor app-insights query --app safeexchange-staging-insights -g safeexchange-staging --analytics-query "customEvents | where name == 'AttachmentUploadSucceeded' and timestamp > ago(4h) | summarize count() by mode=tostring(customDimensions['mode'])"
```

Wait until `mode=legacy` is near zero.

- [ ] **Step 2: Flip the flag in Key Vault / App Configuration** (use the same path the backend binds from). After flip, no redeploy needed.

- [ ] **Step 3: Smoke-test old client** (expect the attachment upload to fail cleanly — main secrets still work).

### Task 13.2: Final report (written back to the conversation)

- [ ] **Step 1: Write a brief summary for the user** covering:
  - Branch names in both repos + final commit SHAs
  - Deploy timestamps (staging backend + staging client + any prod deploys)
  - Playwright test results
  - AI telemetry snapshot: first `AttachmentUploadSucceeded` hashed-mode event vs. first `AttachmentVerified`
  - Any deviations from the spec

---

## Self-review

Ran a fresh read-through against the spec:

1. **Spec §1 summary** → covered by Phases 1 (SerializableSha256), 6 (chunk upload revision), 7 (commit endpoint), 10–11 (client verified download + UI).
2. **Spec §3 threat model table** → catch-by-layer covered in Phase 6 (per-chunk), Phase 7 (whole-content commit), Phase 10 (download-side verify).
3. **Spec §4.2 main-vs-attachment boundary** → the `SafeExchangeContentCommit.Run` checks `existingContent.IsMain` and 422s; the hashed-mode chunk handler is reached only for attachments via the mode-resolver (main path stays in the existing `UploadMainContentAsync`).
4. **Spec §5.1 headers** → `ChunkHashHeaderName` added Task 6.2; `X-SafeExchange-Ticket` reused.
5. **Spec §5.2 commit endpoint** → Task 7.2.
6. **Spec §5.4 DB schema** → Tasks 2.1 + 2.2.
7. **Spec §6.1 client upload flow** → Task 9.3.
8. **Spec §6.2 server chunk handler + mode-lock** → Tasks 4 + 6.
9. **Spec §6.3 server commit** → Task 7.
10. **Spec §6.4 purger lifecycle** → Task 8.1.
11. **Spec §7 download flow + §7.3 local verify** → Tasks 10 + 11.4.
12. **Spec §8 UI** → Task 11.2 + 11.3 + 11.4.
13. **Spec §9 hand-rolled SHA-256** → Phase 1 in full with test battery.
14. **Spec §10 rollout + flags** → Task 3.1 + Task 8.2 (deploy) + Task 12.1 (client deploy) + Task 13.1 (flip).
15. **Spec §11 testing** → per-phase tests + Phase 12 E2E.

No placeholders, no references to undefined types: `SerializableSha256`, `HashingReadStream`, `UploadMode[Resolver]`, `ChunkHashMismatch`, `ContentCommitOutput`, `VerifiedDownloadHelper`, `VerifiedDownloadResult`, `LocalFileVerifier` all defined in their own tasks before first use. Method signatures consistent across tasks (`CommitContentAsync`, `DownloadAsync`, `ComputeHashAsync`). The only variable point is the exact shape of the existing `MigrationItem0000N.cs` pattern (Task 2.2) — that is flagged as "inspect before writing" because migrations in this project are hand-written per item and the exact base class / helper varies.
