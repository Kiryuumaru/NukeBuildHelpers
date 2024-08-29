using Nuke.Common.IO;
using System.Security.Cryptography;

namespace NukeBuildHelpers.Common;

public static partial class AbsolutePathExtensions
{
    /// <summary>
    /// Computes the hash of the file at the specified <see cref="AbsolutePath"/> using the provided <see cref="HashAlgorithm"/>.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the file to hash.</param>
    /// <param name="hashAlgorithm">The hash algorithm to use.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a hexadecimal string.</returns>
    public static async Task<string> GetHash(this AbsolutePath absolutePath, HashAlgorithm hashAlgorithm, CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(absolutePath);
        byte[] hashBytes = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Computes the MD5 hash of the file at the specified <see cref="AbsolutePath"/>.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the file to hash.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the MD5 hash as a hexadecimal string.</returns>
    public static Task<string> GetHashMD5(this AbsolutePath absolutePath, CancellationToken cancellationToken = default)
    {
        return GetHash(absolutePath, MD5.Create(), cancellationToken);
    }

    /// <summary>
    /// Computes the SHA1 hash of the file at the specified <see cref="AbsolutePath"/>.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the file to hash.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the SHA1 hash as a hexadecimal string.</returns>
    public static Task<string> GetHashSHA1(this AbsolutePath absolutePath, CancellationToken cancellationToken = default)
    {
        return GetHash(absolutePath, SHA1.Create(), cancellationToken);
    }

    /// <summary>
    /// Computes the SHA256 hash of the file at the specified <see cref="AbsolutePath"/>.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the file to hash.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the SHA256 hash as a hexadecimal string.</returns>
    public static Task<string> GetHashSHA256(this AbsolutePath absolutePath, CancellationToken cancellationToken = default)
    {
        return GetHash(absolutePath, SHA256.Create(), cancellationToken);
    }

    /// <summary>
    /// Computes the SHA512 hash of the file at the specified <see cref="AbsolutePath"/>.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the file to hash.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the SHA512 hash as a hexadecimal string.</returns>
    public static Task<string> GetHashSHA512(this AbsolutePath absolutePath, CancellationToken cancellationToken = default)
    {
        return GetHash(absolutePath, SHA512.Create(), cancellationToken);
    }
}
