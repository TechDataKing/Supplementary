using System.Windows;

using TechKingPOS.App.Data;
using TechKingPOS.App.Models;

namespace TechKingPOS.App.Services;

public static class SyncService
{
    public static async Task SyncPendingAsync()
    {   var cloud = CloudSettingsRepository.Get();

        if (cloud == null)
            return;

        var rows =
            SyncQueueRepository.GetPending();
            //MessageBox.Show($"Pending Rows: {rows.Count}");

        foreach (var row in rows)
        {
            try
            {
            

                var request = new SyncUploadRequest
                {
                    BusinessCode = row.BusinessCode,
                    DeviceId = cloud.DeviceId.ToString(),
                    TableName = row.TableName,
                    ActionType = row.Action,
                    RecordId = row.RecordId.ToString(),
                    JsonData = row.JsonData
                };
                //MessageBox.Show("Uploading " + row.TableName);

                var response = await ApiClient.PostAsync(
                    ApiRoutes.SyncUpload,
                    request);
                    // MessageBox.Show(
                    //     $"Success={response.Success}\nMessage={response.Message}");

                if (response.Success)
                {
                    SyncQueueRepository.MarkSynced(row.Id);

                    CloudSettingsRepository.UpdateLastSync();
                }
                else
                {
                    SyncQueueRepository.MarkFailed(
                        row.Id,
                        response.Message);
                }
            }
            catch (Exception ex)
            {
                SyncQueueRepository.MarkFailed(
                    row.Id,
                    ex.Message);
            }
        }
    }
}