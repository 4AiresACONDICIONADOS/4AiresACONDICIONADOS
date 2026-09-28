namespace BreathOfEclipse.Data
{
    /// <summary>Roman numerals for form numbers (I … XI and beyond) and Spanish ordinal calls.</summary>
    public static class Roman
    {
        private static readonly int[] Values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
        private static readonly string[] Symbols = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };

        private static readonly string[] SpanishOrdinals =
        {
            "Primera", "Segunda", "Tercera", "Cuarta", "Quinta", "Sexta", "Séptima", "Octava", "Novena", "Décima", "Undécima", "Duodécima"
        };

        public static string Of(int number)
        {
            if (number <= 0) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Values.Length; i++)
                while (number >= Values[i])
                {
                    number -= Values[i];
                    sb.Append(Symbols[i]);
                }
            return sb.ToString();
        }

        /// <summary>"Primera Postura", "Séptima Postura" … (falls back to the numeral past the table).</summary>
        public static string SpanishFormCall(int number) =>
            number >= 1 && number <= SpanishOrdinals.Length ? SpanishOrdinals[number - 1] + " Postura" : "Postura " + Of(number);
    }
}
