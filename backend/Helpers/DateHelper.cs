/*
 * File: DateHelper.cs
 * Provides helper methods for date-based authorization and edit window logic.
 */

namespace backend.Helpers
{
    // =========================================================
    // DATE HELPER INTERFACE
    // Defines contract for date-related authorization checks
    // =========================================================
    public interface IDateHelper
    {
        // Checks if the current date is within the editing window
        bool IsEditWindowOpen();
    }

    // =========================================================
    // DATE HELPER
    // Implements date-based authorization logic for KPI editing
    // =========================================================
    public class DateHelper : IDateHelper
    {
        // Determines if Platform KPI editing is allowed
        // Edit window is open from 1st to 20th of each month
        public bool IsEditWindowOpen()
        {
            return DateTime.Now.Day <= 20;
        }
    }
}
