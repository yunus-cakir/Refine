using System.IO;

namespace Refine.App.Services
{
    public static class DbConstants
    {
        public const string DatabaseFilename = "RefineParams.db3";

        public const SQLite.SQLiteOpenFlags Flags =
            // Veritabanını okuma/yazma modunda aç
            SQLite.SQLiteOpenFlags.ReadWrite |
            // Yoksa oluştur
            SQLite.SQLiteOpenFlags.Create;

        public static string DatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
    }
}