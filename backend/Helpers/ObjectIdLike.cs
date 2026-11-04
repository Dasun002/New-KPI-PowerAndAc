/*
 * File: ObjectIdLike.cs
 * DEPRECATED: Generates MongoDB ObjectID-like identifiers (24-hex format).
 * NOTE: This file is LEGACY and currently UNUSED in the project.
 * The project uses SQL Server with int/long IDENTITY primary keys.
 * Consider removing this file as it serves no purpose in the current architecture.
 */

using System.Security.Cryptography;

namespace backend.Helpers
{
    // =========================================================
    // OBJECT ID LIKE (DEPRECATED)
    // Legacy helper for generating MongoDB-style IDs
    // SHOULD BE REMOVED - Not used in current SQL Server implementation
    // =========================================================
    public static class ObjectIdLike
    {
        // Generates a random 24-character hexadecimal string
        // Similar format to MongoDB ObjectID but not compatible
        // NOTE: This method is not used anywhere in the project
        public static string NewId24()
        {
            // Generate 12 random bytes => 24 hex characters when converted
            byte[] bytes = RandomNumberGenerator.GetBytes(12);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
