namespace OCPP.Core.Server.Tests.Infrastructure
{
    /// <summary>
    /// Paths to files in the repository (e.g. SQL scripts)
    /// </summary>
    public static class RepositoryPaths
    {
        /// <summary>
        /// Root directory of the repository (contains "OCPP.Core.sln")
        /// </summary>
        public static string Root { get; } = FindRoot();

        public static string File(params string[] parts) => Path.Combine(new[] { Root }.Concat(parts).ToArray());

        private static string FindRoot()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !System.IO.File.Exists(Path.Combine(dir.FullName, "OCPP.Core.sln")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root (OCPP.Core.sln) not found");
        }
    }
}
