using System.IO;
using System.Text;

namespace BingWallTray.App.Utils
{
    public static class FileNameSanitizer
    {
        public static string Sanitize(string fileName, char replacement = '_')
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "bing-wallpaper";
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(fileName.Length);

            foreach (char c in fileName)
            {
                bool isValid = true;
                foreach (char invalid in invalidChars)
                {
                    if (c == invalid)
                    {
                        isValid = false;
                        break;
                    }
                }
                sb.Append(isValid ? c : replacement);
            }

            return sb.ToString();
        }
    }
}
