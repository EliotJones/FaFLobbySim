namespace FaFLobbySimClient;

internal delegate Occupancy? CalculateLobbyOccupancy(IReadOnlyList<WordRegion> regions, WidthHeight widthHeight);

internal class CalculateLobbyOccupancyHandler
{
    public Occupancy? Calculate(IReadOnlyList<WordRegion> regions, WidthHeight widthHeight)
    {
        var thresholdY = widthHeight.Width * 0.5f;

        var lines = new List<List<WordRegion>>();

        var consumedIndices = new HashSet<int>();
        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];

            // Don't let right hand side of screen serve as pivot for grouping into lines
            if (consumedIndices.Contains(i) || region.Bounds.TopLeft.X >= thresholdY)
            {
                continue;
            }

            consumedIndices.Add(i);


            var resultList = new List<WordRegion>
            {
                region
            };

            for (int j = 0; j < regions.Count; j++)
            {
                if (consumedIndices.Contains(j))
                {
                    continue;
                }
                var otherRegion = regions[j];

                var dy = Math.Abs(region.Bounds.BottomRight.Y - otherRegion.Bounds.BottomRight.Y);

                if (dy <= 5)
                {
                    resultList.Add(otherRegion);
                    consumedIndices.Add(j);
                }
                else if (Math.Abs(region.Bounds.TopLeft.Y - otherRegion.Bounds.TopLeft.Y) < 2 && dy < 10)
                {
                    resultList.Add(otherRegion);
                    consumedIndices.Add(j);
                }
                else if (otherRegion.Bounds.TopLeft.Y >= region.Bounds.TopLeft.Y
                         && otherRegion.Bounds.BottomRight.Y <= region.Bounds.BottomRight.Y)
                {
                    resultList.Add(otherRegion);
                    consumedIndices.Add(j);
                }
            }

            lines.Add(resultList.OrderBy(x => x.Bounds.TopLeft.X).ToList());
        }

        lines = lines.OrderBy(x => x[0].Bounds.TopLeft.Y).ToList();

        // Table headers are in the top 30% of the screen and occupy more than 50% of the width.
        const int topPercent = 30;
        const int widthPercent = 45;

        var yThreshold = widthHeight.Height * (topPercent / 100f);
        var requiredWidth = widthHeight.Width * (widthPercent / 100f);

        List<WordRegion>? possibleHeaderRow = null;
        foreach (var line in lines)
        {
            var maxY = line.Max(x => x.Bounds.BottomRight.Y);
            var minX = line.Min(x => x.Bounds.TopLeft.X);
            var maxX = line.Max(x => x.Bounds.BottomRight.X);
            var width = maxX - minX;

            if (line.Count < 6)
            {
                continue;
            }

            if (maxY < yThreshold && width > requiredWidth)
            {
                possibleHeaderRow = line;
                break;
            }
        }

        if (possibleHeaderRow == null)
        {
            return null;
        }

        // Split the screen into a table under the words, one word per-column.
        var columnXs = new List<(int left, int right)>();
        for (var i = 0; i < possibleHeaderRow.Count; i++)
        {
            var region = possibleHeaderRow[i];

            // Only select the column starting 'Nickname', a word of length > 50 pixels
            if (region.Bounds.Width < 50)
            {
                continue;
            }

            // Skip the last 2 columns, they're probably garbage noise
            if (possibleHeaderRow.Count > 5 && i >= possibleHeaderRow.Count - 2)
            {
                continue;
            }

            var endX = i == possibleHeaderRow.Count - 1
                ? region.Bounds.BottomRight.X + 10
                : possibleHeaderRow[i + 1].Bounds.TopLeft.X - 1;

            columnXs.Add((region.Bounds.TopLeft.X - 1, endX));
        }

        var rowCount = 0;
        var occupied = 0;
        var headerIndex = lines.IndexOf(possibleHeaderRow);
        for (var i = 0; i < lines.Count; i++)
        {
            if (i <= headerIndex)
            {
                continue;
            }

            var line = lines[i];
            for (var j = 0; j < columnXs.Count; j++)
            {
                var column = columnXs[j];

                var words = line.Where(x =>
                    x.Bounds.TopLeft.X >= column.left && x.Bounds.BottomRight.X <= column.right)
                    .ToList();

                // A single word with no chevron, either the word 'Closed' or a single word username.
                if (j == 0 && words.Count == 1)
                {
                    rowCount++;
                    occupied++;
                    break;
                }

                if (j == 0 && words.Count == 0)
                {
                    break;
                }
                
                if (j == 0 && words.Count > 0)
                {
                    rowCount++;
                }

                if (j > 0 && words.Count > 0)
                {
                    // A 'word' in another column is team number, faction logo, color block, CPU indicator, etc.
                    occupied++;
                }
            }
        }

        return new Occupancy(rowCount, occupied);
    }
}