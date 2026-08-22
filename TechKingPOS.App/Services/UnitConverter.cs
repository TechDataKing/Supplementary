namespace TechKingPOS.App.Services
{
    public static class UnitConverter
    {
        /// <summary>
        /// Converts any supported input unit into BASE units:
        /// pieces, kg, l
        /// </summary>
        public static bool TryToBase(
            string inputUnit,
            decimal quantity,
            decimal unitValue,
            out string baseUnit,
            out decimal baseQty,
            out string error
        )
        {
            baseUnit = null!;
            baseQty = 0;
            error = null!;

            if (string.IsNullOrWhiteSpace(inputUnit))
            {
                error = "Unit type is required";
                return false;
            }

            if (quantity <= 0)
            {
                error = "Quantity value must be greater than zero";
                return false;
            }

            inputUnit = inputUnit.Trim().ToLower();

            switch (inputUnit)
            {
                // ================= COUNT =================
                case "piece":
                case "pieces":
                    baseUnit = "pieces";
                    baseQty = quantity; // always 1:1
                    return true;

                // ================= WEIGHT =================
                case "g":
                case "gram":
                case "grams":
                    baseUnit = "kg";
                    baseQty = (quantity * unitValue) / 1000m;
                    return true;

                case "kg":
                    baseUnit = "kg";
                    baseQty = quantity * unitValue;
                    return true;

                // ================= VOLUME =================
                case "ml":
                    baseUnit = "l";
                    baseQty = (quantity * unitValue) / 1000m;
                    return true;

                case "l":
                    baseUnit = "l";
                    baseQty = quantity * unitValue;
                    return true;
                                    // ================= LENGTH (METRIC) =================
                case "mm":
                case "millimeter":
                case "millimeters":
                    baseUnit = "m";
                    baseQty = (quantity * unitValue) / 1000m;
                    return true;

                case "cm":
                case "centimeter":
                case "centimeters":
                    baseUnit = "m";
                    baseQty = (quantity * unitValue) / 100m;
                    return true;

                case "m":
                case "meter":
                case "meters":
                    baseUnit = "m";
                    baseQty = quantity * unitValue;
                    return true;


                // ================= LENGTH (IMPERIAL) =================
                case "ft":
                case "foot":
                case "feet":
                case "fts":
                    baseUnit = "fts";
                    baseQty = quantity * unitValue;
                    return true;

                case "inch":
                case "in":
                case "inches":
                    baseUnit = "inches";
                    baseQty = quantity * unitValue;
                    return true;

                default:
                    error = $"Unsupported unit: {inputUnit}";
                    return false;
            }
        }
    }
}
