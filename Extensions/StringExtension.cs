using System.Text;

namespace ModuWeb.Extensions
{
    public static class StringExtension
    {
        public static string Replace(this string original, string oldValue, string newValue, int count)
        {
            if (count == 0)
                return original;

            if (original == null) throw new ArgumentNullException(nameof(original));
            if (oldValue == null) throw new ArgumentNullException(nameof(oldValue));
            if (newValue == null) throw new ArgumentNullException(nameof(newValue));
            if (oldValue.Length == 0)
                throw new ArgumentException("Old value cannot be empty", nameof(oldValue));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");

            StringBuilder sb = new StringBuilder(original.Length);
            int startIndex = 0;
            int replacements = 0;

            while (replacements < count)
            {
                int nextIndex = original.IndexOf(oldValue, startIndex, StringComparison.Ordinal);
                if (nextIndex == -1) break;

                sb.Append(original, startIndex, nextIndex - startIndex);
                sb.Append(newValue);
                startIndex = nextIndex + oldValue.Length;
                replacements++;
            }

            sb.Append(original, startIndex, original.Length - startIndex);
            return sb.ToString();
        }
    }

}
