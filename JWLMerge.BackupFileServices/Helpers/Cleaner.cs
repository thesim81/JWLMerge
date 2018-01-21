﻿namespace JWLMerge.BackupFileServices.Helpers
{
    using System.Collections.Generic;
    using System.Linq;
    using Models;
    using Serilog;

    /// <summary>
    /// Cleans jwlibrary files by removing redundant or anomalous database rows.
    /// </summary>
    internal class Cleaner
    {
        private readonly BackupFile _backupFile;

        public Cleaner(BackupFile backupFile)
        {
            _backupFile = backupFile;
        }

        /// <summary>
        /// Cleans the data, removing unused rows.
        /// </summary>
        /// <returns>Number of rows removed.</returns>
        public int Clean()
        {
            return CleanBlockRanges() + CleanLocations();
        }

        private HashSet<int> GetUserMarkIdsInUse()
        {
            var result = new HashSet<int>();
            
            foreach (var userMark in _backupFile.Database.UserMarks)
            {
                result.Add(userMark.UserMarkId);
            }

            return result;
        }

        private HashSet<int> GetLocationIdsInUse()
        {
            var result = new HashSet<int>();

            foreach (var bookmark in _backupFile.Database.Bookmarks)
            {
                result.Add(bookmark.LocationId);
                result.Add(bookmark.PublicationLocationId);
            }
            
            foreach (var note in _backupFile.Database.Notes)
            {
                if (note.LocationId != null)
                {
                    result.Add(note.LocationId.Value);
                }
            }

            foreach (var userMark in _backupFile.Database.UserMarks)
            {
                result.Add(userMark.LocationId);
            }

            Log.Logger.Debug($"Found {result.Count} location Ids in use");
            
            return result;
        }

        /// <summary>
        /// Cleans the locations.
        /// </summary>
        /// <returns>Number of location rows removed.</returns>
        private int CleanLocations()
        {
            int removed = 0;
            
            var locations = _backupFile.Database.Locations;
            if (locations.Any())
            {
                var locationIds = GetLocationIdsInUse();

                foreach (var location in Enumerable.Reverse(locations))
                {
                    if (!locationIds.Contains(location.LocationId))
                    {
                        Log.Logger.Debug($"Removing redundant location id: {location.LocationId}");
                        locations.Remove(location);
                        ++removed;
                    }
                }
            }

            return removed;
        }

        /// <summary>
        /// Cleans the block ranges.
        /// </summary>
        /// <returns>Number of ranges removed.</returns>
        private int CleanBlockRanges()
        {
            int removed = 0;

            var userMarkIdsFound = new HashSet<int>();
            
            var ranges = _backupFile.Database.BlockRanges;
            if (ranges.Any())
            {
                var userMarkIds = GetUserMarkIdsInUse();
                
                foreach (var range in Enumerable.Reverse(ranges))
                {
                    if (!userMarkIds.Contains(range.UserMarkId))
                    {
                        Log.Logger.Debug($"Removing redundant range: {range.BlockRangeId}");
                        ranges.Remove(range);
                        ++removed;
                    }
                    else
                    {
                        if (userMarkIdsFound.Contains(range.UserMarkId))
                        {
                            // don't know how to handle this situation - we are expecting 
                            // a unique constraint on the UserMarkId column but have found 
                            // occasional duplication!
                            Log.Logger.Debug($"Removing redundant range (duplicate UserMarkId): {range.BlockRangeId}");
                            ranges.Remove(range);
                            ++removed;
                        }
                        else
                        {
                            userMarkIdsFound.Add(range.UserMarkId);
                        }
                    }
                }
            }

            return removed;
        }
    }
}