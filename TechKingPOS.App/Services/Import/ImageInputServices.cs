using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;
using TechKingPOS.App.Models;

namespace TechKingPOS.App.Services.Import
{
    public class ImageInputService
    {
        public async Task<ImageInputResult> PickFromFileAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png"
            };

            if (dialog.ShowDialog() == true)
            {
                var bytes = await File.ReadAllBytesAsync(dialog.FileName);

                return new ImageInputResult
                {
                    FilePath = dialog.FileName,
                    ImageBytes = bytes,
                    Source = "File"
                };
            }

            return null;
        }
    }
}