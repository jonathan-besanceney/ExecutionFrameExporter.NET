namespace FrameExporter.Utils
{
    public static class Common
    {
        public static bool EndsWithAny(string f, string[] matches)
        {
            foreach (string match in matches)
                if (f.EndsWith(match))
                    return true;
            return false;
        }
    }
}
