using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
//using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using File = Google.Apis.Drive.v3.Data.File;

namespace DBBackupLib
{
    public class GoogleApis
    {
        static string[] Scopes = { DriveService.Scope.Drive };
        static string ApplicationName = "Backup Database";
        static string FileUploadDescription = "File uploaded by Golden Backup Utility";

        public static bool IsUploadingFile { get; private set; }

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

            using (var stream =
                new FileStream("BackupDBCredential.json", FileMode.Open, FileAccess.Read))
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

        public static async Task<IUploadProgress> UploadFile(string uploadFile)
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
                body.MimeType = GetMimeType(uploadFile);
                try
                {
                    // File's content.
                    byte[] byteArray = System.IO.File.ReadAllBytes(uploadFile);
                    MemoryStream stream = new System.IO.MemoryStream(byteArray);
                    FilesResource.CreateMediaUpload uploadRequest = service.Files.Create(body, stream, body.MimeType);
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

        public static void ListFiles()
        {
            UserCredential credential;

            using (var stream =
                new FileStream("BackupDBCredential.json", FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            // Define parameters of request.
            FilesResource.ListRequest listRequest = service.Files.List();
            listRequest.PageSize = 10;
            listRequest.Fields = "nextPageToken, files(id, name)";

            // List files.
            IList<Google.Apis.Drive.v3.Data.File> files = listRequest.Execute()
                .Files;
            Console.WriteLine("Files:");
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    Console.WriteLine("{0} ({1})", file.Name, file.Id);
                }
            }
            else
            {
                Console.WriteLine("No files found.");
            }
            Console.Read();

        }

    }


}
