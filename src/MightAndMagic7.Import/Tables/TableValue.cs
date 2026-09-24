using System.Globalization;
using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tables;

/// <summary>
/// Field readers that fail with the table, row, and column named, so a malformed table points at the
/// data rather than at a parse exception.
/// </summary>
internal static class TableValue
{
    internal static int Integer(TabularTable table, TabularRow row, int column, string columnName)
    {
        string text = row.Field(column).Trim();
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new LodFormatException($"{table.Source}: row {row.Number} column {columnName} is '{text}', not a number.");
        }

        return value;
    }

    /// <summary>
    /// Reads an integer column whose values may be written with thousands separators and padding
    /// inside quotes, as the monster table writes its hit points and experience.
    /// </summary>
    internal static int ThousandsInteger(TabularTable table, TabularRow row, int column, string columnName)
    {
        string text = row.Field(column).Replace(",", string.Empty, StringComparison.Ordinal).Trim();
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new LodFormatException($"{table.Source}: row {row.Number} column {columnName} is '{row.Field(column)}', not a number.");
        }

        return value;
    }

    /// <summary>
    /// Reads a column that is usually a number and may carry free text instead. A non-numeric cell is
    /// data, not a defect: the training cap column writes "No Max" for an uncapped hall, and the
    /// caller keeps the raw text so the ruleset can read it.
    /// </summary>
    internal static int? NumericOrNull(TabularRow row, int column) =>
        int.TryParse(row.Field(column).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : null;

    internal static int? OptionalInteger(TabularTable table, TabularRow row, int column, string columnName)
    {
        string text = row.Field(column).Trim();
        if (text.Length == 0) return null;
        return Integer(table, row, column, columnName);
    }

    internal static double? OptionalDecimal(TabularTable table, TabularRow row, int column, string columnName)
    {
        string text = row.Field(column).Trim();
        if (text.Length == 0) return null;
        return Decimal(table, row, column, columnName);
    }

    internal static double Decimal(TabularTable table, TabularRow row, int column, string columnName)
    {
        string text = row.Field(column).Trim();
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            throw new LodFormatException($"{table.Source}: row {row.Number} column {columnName} is '{text}', not a number.");
        }

        return value;
    }

    internal static string Text(TabularRow row, int column) => row.Field(column).Trim();
}
