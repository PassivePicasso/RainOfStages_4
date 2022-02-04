using System.IO;
using System.Linq;

namespace PassivePicasso.RainOfStages.Plugin.Utility
{
    public static class PathHelper
    {
        private static readonly string[] rootPath = new[] { "Packages", "twiner-rainofstages", "plugins", "RainOfStages" };
        public static string RoSPath(params string[] path)
        {
            var paths = rootPath.Union(path).ToArray();

            return ProjectPath(paths);
        }

        public static string ProjectPath(params string[] path)
        {
            return Path.Combine(path).Replace("\\","/");
        }
    }
}