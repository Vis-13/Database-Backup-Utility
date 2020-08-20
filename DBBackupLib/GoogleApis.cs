using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
//using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Markup;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util;
using Google.Apis.Util.Store;
using Reactive.Bindings.ObjectExtensions;
using File = Google.Apis.Drive.v3.Data.File;

namespace DBBackupLib
{
    public class GoogleApis
    {
        static string[] Scopes = { DriveService.Scope.Drive };
        static string ApplicationName = "Backup Database";
        static string FileUploadDescription = "File uploaded by Golden Backup Utility";
        static string UploadIdKey = "UpId";
        public const string GFolderId = "18-wjFxMidgdy9eJbZSY1xCtPIYR8cDgI";

        public static bool IsUploadingFile { get; private set; }
        public static bool IsDeletingFile { get; private set; }
        public static bool IsEmptyingTrash { get; private set; }
        public static bool IsListingFileWithFilter { get; private set; }

        /// a Valid authenticated DriveService
        /// The title of the file. Used to identify file or folder name.
        /// A short description of the file.
        /// Collection of parent folders which contain this file. 
        ///                       Setting this field will put the file in all of the provided folders. root folder.        /// 
        //public static File createDirectory(DriveService _service, string _title, string _description, string _parent)
        //{

        //    File NewDirectory = null;

        //    // Create metaData for a new Directory
        //    File body = new File();
        //    body.Name = _title;
        //    body.Description = _description;
        //    body.MimeType = "application/vnd.google-apps.folder";
        //    //body.Parents = new List() { new ParentReference() { Id = _parent } };
        //    try
        //    {
        //        FilesResource.InsertRequest request = _service.Files.Insert(body);
        //        NewDirectory = request.Execute();
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine("An error occurred: " + e.Message);
        //    }

        //    return NewDirectory;
        //}

        //tries to figure out the mime type of the file.
        private static string GetMimeType(string fileName)
        {
            string mimeType = "application/octet-stream";// "application/unknown";
            string ext = System.IO.Path.GetExtension(fileName).ToLower();
            //Microsoft.Win32.re regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
            //if (regKey != null && regKey.GetValue("Content Type") != null)
            //    mimeType = regKey.GetValue("Content Type").ToString();
            return mimeType;
        }

        /// Uploads a file
        /// Documentation: https://developers.google.com/drive/v2/reference/files/insert
        /// 
        /// a Valid authenticated DriveService
        /// path to the file to upload
        /// Collection of parent folders which contain this file. 
        ///                       Setting this field will put the file in all of the provided folders. root folder.
        /// If upload succeeded returns the File resource of the uploaded file 
        ///          If the upload fails returns null
        //        public static File UploadFile(DriveService _service, string _uploadFile, string _parent)
        //        {

        //            if (System.IO.File.Exists(_uploadFile))
        //            {
        //                File body = new File();
        //                body.Name = System.IO.Path.GetFileName(_uploadFile);
        //                body.Description = "File uploaded by Diamto Drive Sample";
        //                body.MimeType = GetMimeType(_uploadFile);
        //                //body.Parents = new List() { new ParentReference() { Id = _parent } };

        //                // File's content.
        //                byte[] byteArray = System.IO.File.ReadAllBytes(_uploadFile);
        //                System.IO.MemoryStream stream = new System.IO.MemoryStream(byteArray);
        //                try
        //                {
        //                    FilesResource.CreateRequest request = _service.Files.Insert(body, stream, GetMimeType(_uploadFile));
        //                    request.Upload();
        //                    return request.ResponseBody;
        //                }
        //                catch (Exception e)
        //                {
        //                    Console.WriteLine("An error occurred: " + e.Message);
        //                    return null;
        //                }
        //            }
        //            else
        //            {
        //                Console.WriteLine("File does not exist: " + _uploadFile);
        //                return null;
        //            }

        //        }

        public static UserCredential GetUserCredential() {
            UserCredential credential;
            using (var stream = new FileStream("BackupDBCredential.json", FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(GoogleClientSecrets.Load(stream).Secrets, Scopes, "user", CancellationToken.None, new FileDataStore(credPath, true)).Result;
                Debug.WriteLine("Credential file saved to: " + credPath);
            }
            return credential;
        }

        public static DriveService GetGDriveService() {
            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = GetUserCredential(),
                ApplicationName = ApplicationName,
            });
            return service;
        }

        public static async Task<IUploadProgress> UploadFile(string uploadFile, Dictionary<string, string> appFileAttributes = null)
        {
            IsUploadingFile = true;
            DriveService service = GetGDriveService();
            IUploadProgress progress = null;
            //Task<IUploadProgress> progress = null;
            if (System.IO.File.Exists(uploadFile))
            {
                File body = new File();
                body.Name = Path.GetFileName(uploadFile);
                _uploadFilename = body.Name;
                body.Description = FileUploadDescription;
                body.Properties = appFileAttributes;
                body.MimeType = GetMimeType(uploadFile);
                //body.Id = DateTime.Now.ToString("dd-HH:mm:ss.fffff");


                try
                {
                    // File's content.
                    byte[] byteArray = System.IO.File.ReadAllBytes(uploadFile);
                    MemoryStream stream = new MemoryStream(byteArray);

                    //FilesResource.UpdateMediaUpload uploadRequest = service.Files.Update(f, "", stream, body.MimeType);
                    FilesResource.CreateRequest cr = service.Files.Create(body);
                    File f = cr.Execute();
                    //FilesResource.CreateMediaUpload uploadRequest = service.Files.Create(f, stream, body.MimeType);
                    //uploadRequest.Body.Id = DateTime.Now.ToString("dd-HH:mm:ss.fffff");
                    //body.Id = "WRJskjslkdjslj3q9";
                    FilesResource.CreateMediaUpload uploadRequest = service.Files.Create(body, stream, body.MimeType);
                    //FilesResource.CreateMediaUpload ur = new FilesResource.CreateMediaUpload(service, body, stream, body.MimeType);
                    //uploadRequest.Fields = "Id";           
                    //uploadRequest.Body.Properties = new Dictionary<string, string>();
                    //uploadRequest.Body.Properties.Add("sdkjfs", "kdjskjd");
                    uploadRequest.SupportsTeamDrives = true;
                    uploadRequest.ResponseReceived += UploadRequest_ResponseReceived;
                    uploadRequest.ProgressChanged += UploadRequest_ProgressChanged;
                    // new Exception("Exception");
                    progress = await uploadRequest.UploadAsync();//.UploadAsync();
                }
                catch (Exception e)
                {
                    Debug.WriteLine("An error occurred: " + e.Message);
                    throw;
                }
                finally { IsUploadingFile = false; }
            }
            service?.Dispose();
            return progress;// uploadedFile == null ? string.Empty : uploadedFile.Id;
        }

        public static IUploadProgress UploadFile(string uploadFile, ref File file, Dictionary<string, string> appFileAttributes = null)
        {
            IsUploadingFile = true;
            DriveService service = GetGDriveService();
            IUploadProgress progress = null;
            if (System.IO.File.Exists(uploadFile))
            {
                File body = new File();
                body.Name = Path.GetFileName(uploadFile);
                _uploadFilename = body.Name;
                body.Description = FileUploadDescription;
                body.Properties = appFileAttributes;
                body.MimeType = GetMimeType(uploadFile);
                body.Parents = new List<string>(new[] { GFolderId });
                //body.Parents.Add(/*"Golden Database Backup(18 - */"wjFxMidgdy9eJbZSY1xCtPIYR8cDgI");
                try
                {
                    // File's content.
                    byte[] byteArray = System.IO.File.ReadAllBytes(uploadFile);
                    MemoryStream stream = new MemoryStream(byteArray);
                    FilesResource.CreateMediaUpload uploadRequest = service.Files.Create(body, stream, body.MimeType);
                    uploadRequest.Body.Properties = appFileAttributes;
                    //uploadRequest.Body.Properties.Add("sdkjfs", "kdjskjd");
                    uploadRequest.SupportsTeamDrives = true;
                    uploadRequest.ResponseReceived += UploadRequest_ResponseReceived;
                    uploadRequest.ProgressChanged += UploadRequest_ProgressChanged;
                    // new Exception("Exception");
                    progress = uploadRequest.Upload();//.UploadAsync();
                    if (progress.Status == UploadStatus.Completed)
                        file = uploadRequest.ResponseBody;
                }
                catch (Exception e)
                {
                    Debug.WriteLine("An error occurred: " + e.Message);
                    throw;
                }
                finally { IsUploadingFile = false; }
            }
            service?.Dispose();
            return progress;// uploadedFile == null ? string.Empty : uploadedFile.Id;
        }

        private static void UploadRequest_ResponseReceived(File obj)
        {

        }

        static bool _hasProgressChangedStarted = false;
        static string _uploadFilename = string.Empty;
        private static void UploadRequest_ProgressChanged(IUploadProgress obj)
        {
            if (!_hasProgressChangedStarted) {
                _hasProgressChangedStarted = true;
                System.IO.File.AppendAllLines("Bytes Sent.txt", new string[] { $"************************** {_uploadFilename} ***************************" });
            }
            System.IO.File.AppendAllLines("Bytes Sent.txt", new string[] { "Bytes Sent: " + obj.BytesSent + "       at " + DateTime.Now.ToString("MM-dd-yyyy HH:mm:ss.ffffff") });
            if (obj.Status == UploadStatus.Completed || obj.Status == UploadStatus.Failed)
            {
                _hasProgressChangedStarted = false; _uploadFilename = "";
                System.IO.File.AppendAllLines("Bytes Sent.txt", new string[] { $"************************** {obj.Status} ***************************" });
            }
        }

        public static IList<File> ListFiles()
        {
            // Create Drive API service.
            var service = GetGDriveService();
            // Define parameters of request.
            FilesResource.ListRequest listRequest = service.Files.List();
            listRequest.PageSize = 50;
            listRequest.Fields = "nextPageToken, files(id, name)";
            DriveList driveList = service.Drives.List().Execute();
            driveList.Drives.Select(d => { Debug.WriteLine($"Drive: {d.Name}, Id: {d.Id}"); return d; }).ToList();
            // List files.
            IList<Google.Apis.Drive.v3.Data.File> files = listRequest.Execute().Files;
            Debug.WriteLine("Files:");
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    Debug.WriteLine("{0} ({1})", file.Name, file.Id);
                }
            }
            else
            {
                Debug.WriteLine("No files found.");
            }
            //.Read();
            return files;
        }

        public static Task<string> DeleteFileAsync(string gFileId) {
            IsDeletingFile = true;
            Task<string> deleteTask = null;
            DriveService service = GetGDriveService();
            try
            {
                FilesResource.DeleteRequest deleteRequest = service.Files.Delete(gFileId);
                deleteTask = deleteRequest.ExecuteAsync();                
            }
            catch (Exception e)
            {
                Debug.WriteLine("An error occurred: " + e.Message);
                throw;
            }
            finally { IsDeletingFile = false; }            
            service?.Dispose();
            return deleteTask;
        }

        public static string DeleteFile(string localFilePath, string gFileId)
        {
            IsDeletingFile = true;
            string deleteResult = "";
            if (string.IsNullOrWhiteSpace(gFileId)) {
                deleteResult = "gFileId is null or empty!";
                return deleteResult;
            }
            DriveService service = GetGDriveService();
            try
            {
                FilesResource.DeleteRequest deleteRequest = service.Files.Delete(gFileId);
                deleteResult = deleteRequest.Execute();
            }
            catch (Exception e)
            {
                Debug.WriteLine("An error occurred: " + e.Message);
                throw new Exception($"{localFilePath} could not be deleted from GDrive (Id: {gFileId})", e);
            }
            finally { IsDeletingFile = false; }
            service?.Dispose();
            return deleteResult;
        }

        public static string EmptyTrashFolder() {
            IsEmptyingTrash = true;
            string emptyTrashResult = "Error";
            DriveService service = GetGDriveService();
            try
            {
                FilesResource.EmptyTrashRequest emptyTrashRequest = service.Files.EmptyTrash();
                emptyTrashResult = emptyTrashRequest.Execute();
            }
            catch (GoogleApiException e)
            {
                Debug.WriteLine("An error occurred: " + e.Message);
                throw;
            }
            catch (Exception e) {
                Debug.WriteLine("An error occurred: " + e.Message);
                throw;
            }
            finally { IsEmptyingTrash = false; }
            service?.Dispose();
            return emptyTrashResult;
        }

        public static FileList GetYesterdaysFiles(string filter) {
            IsListingFileWithFilter = true;
            DriveService service = GetGDriveService();
            FileList files = null;
            try
            {
                FilesResource.ListRequest listRequest = service.Files.List();
                DateTime ct = DateTime.Now;
                //ct = ct.TimeOfDay.TotalMinutes < 5 ? ct.AddDays(-1).Date : ct;
                listRequest.Q = filter;
                listRequest.Fields = "files(id, parents, name, createdTime, modifiedTime)";
                files = listRequest.Execute();
            }
            catch (GoogleApiException e)
            {
                Debug.WriteLine("An error occurred: " + e.Message);
                throw;
            }
            catch (Exception e)
            {
                Debug.WriteLine("An error occurred: " + e.Message);
                throw;
            }
            finally { IsListingFileWithFilter = false; }
            service?.Dispose();
            return files;
        }
    }


}
