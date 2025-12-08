// Standalone C# script to generate BCrypt hash
// Run with: dotnet run GenerateHash.cs
using System;

class Program
{
    static void Main()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("admin123!");
        Console.WriteLine($"Hash for 'admin123!': {hash}");
    }
}
