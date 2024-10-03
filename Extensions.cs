using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using NPOI.XWPF.UserModel;

namespace VideoToText
{
    public static class Extensions
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

        public static string RemoveAfterBracket(this string input)
        {
            // Check if the input is null or empty
            if (string.IsNullOrEmpty(input))
                return input;

            // Find the index of the opening bracket
            int index = input.IndexOf('[');

            // If the bracket is found, take the substring before it
            return index != -1 ? input.Substring(0, index).Trim() : input;
        }

        public static string ExtractIdentifier(this string input)
        {
            // Check if the input string is null or empty
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Find the opening and closing brackets
            int startIndex = input.IndexOf('[');
            int endIndex = input.IndexOf(']', startIndex);

            // If brackets are found, extract the substring between them
            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                return input.Substring(startIndex + 1, endIndex - startIndex - 1);
            }

            // Return an empty string if no valid identifier is found
            return string.Empty;
        }

        public static Text Append(this Run run, string text)
        {
            Text lastText = null;

            if (text == null)
            {
                lastText = new Text();

                run.Append(lastText);

                return lastText;
            }

            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int index = 0; index < lines.Length; index++)
            {
                if (index != 0)
                    run.AppendBreak();

                var line = lines[index];

                lastText = new Text(line);

                if (line.StartsWith(" ") || line.EndsWith(" "))
                    lastText.Space = SpaceProcessingModeValues.Preserve;

                run.Append(lastText);
            }

            return lastText;
        }

        public static void AppendBreak(this Run run)
        {
            run.Append(new Break());
        }

        // Append text to a Run with automatic line breaks
        public static void AppendTextForNPOI(this XWPFRun run, string text)
        {
            if (text == null)
            {
                return; // Do nothing for null text
            }

            // Split the text into lines based on different line break characters
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int index = 0; index < lines.Length; index++)
            {
                var line = lines[index];

                // If not the first line, insert a line break
                if (index > 0)
                {
                    run.AddCarriageReturn(); // Add a line break
                }

                // Add the text of the line
                run.AppendText(line); // Set the text for the line
            }
        }
    }
}
