/*
 * File: IMultiTableService.cs
 * Service interface and DTOs for fetching multi-table platform data from SOAP UI endpoints.
 * Supports MSAN, VPN, and SLBN data retrieval.
 */

using backend.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace backend.Services
{
    // =========================================================
    // MULTI-TABLE SERVICE INTERFACE
    // Defines contract for fetching platform data from SOAP UI
    // =========================================================
    public interface IMultiTableService
    {
        Task<List<PlatformRecordDto>> FetchMsanDataAsync(int? year = null, int? month = null);
        Task<List<PlatformRecordDto>> FetchVpnDataAsync(int? year = null, int? month = null);
        Task<List<PlatformRecordDto>> FetchSlbnDataAsync(int? year = null, int? month = null);
        Task<List<PlatformRecordDto>> FetchTowerDataAsync(int? year = null, int? month = null);
    }

    // =========================================================
    // PLATFORM RECORD DTO
    // Represents monthly platform data with details
    // =========================================================
    public class PlatformRecordDto
    {
        // Month identifier for the data
        public string Month { get; set; } = string.Empty;

        // Collection of platform details for the month
        public List<PlatformDetailDto> Details { get; set; } = new();
        public Dictionary<string, PlatformDetailDto>? Data { get; internal set; }
    }

    // =========================================================
    // PLATFORM DETAIL DTO
    // Represents individual platform detail columns
    // =========================================================
   
}
