namespace VideoToText
{
    public static class PathHelper
    {
        // Method to remove invalid path characters
        public static string RemoveInvalidPathChars(string path)
        {
            // Get all invalid characters for a path
            char[] invalidChars = Path.GetInvalidPathChars();

            // Add additional characters that might cause issues
            char[] additionalInvalidChars = new char[] { ':', '*', '?', '"', '<', '>', '|', '(', ')' };

            // Combine the invalid characters arrays
            char[] allInvalidChars = invalidChars.Concat(additionalInvalidChars).ToArray();

            // Replace any invalid character with an underscore or any character of your choice
            string cleanedPath = new string(path.Where(c => !allInvalidChars.Contains(c)).ToArray());

            return cleanedPath;
        }
    }
}
