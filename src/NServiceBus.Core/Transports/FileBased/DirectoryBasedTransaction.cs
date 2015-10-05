namespace NServiceBus.Transports.FileBased
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    class DirectoryBasedTransaction
    {
        public DirectoryBasedTransaction(string basePath)
        {
            this.basePath = basePath;
            var transactionId = Guid.NewGuid().ToString();

            transactionDir = Path.Combine(basePath, ".pending", transactionId);
            commitDir = Path.Combine(basePath, ".committed", transactionId);
        }

        public string FileToProcess => fileToProcess;

        public void BeginTransaction(string incomingFilePath)
        {
            Directory.CreateDirectory(transactionDir);
            fileToProcess = Path.Combine(transactionDir, Path.GetFileName(incomingFilePath));
            File.Move(incomingFilePath, fileToProcess);
        }

        public void Commit()
        {
            var dispatchFile = Path.Combine(transactionDir, "dispatch.txt");
            File.WriteAllLines(dispatchFile, outgoingFiles.Select(file => $"{file.TxPath}=>{file.TargetPath}").ToArray());

             Directory.Move(transactionDir, commitDir);
        }


        public void Rollback()
        {
            //rollback by moving the file back to the main dir
            File.Move(fileToProcess, basePath);
            Directory.Delete(transactionDir, true);
        }


        public void Enlist(string messagePath, List<string> messageContents)
        {
            var txPath = Path.Combine(transactionDir, Path.GetFileName(messagePath));
            var committedPath = Path.Combine(commitDir, Path.GetFileName(messagePath));

            File.WriteAllLines(txPath, messageContents);
            outgoingFiles.Add(new OutgoingFile(committedPath, messagePath));
        }


        public void Dipatch()
        {
            foreach (var outgoingFile in outgoingFiles)
            {
                File.Move(outgoingFile.TxPath, outgoingFile.TargetPath);
            }

            Directory.Delete(commitDir, true);
        }

        List<OutgoingFile> outgoingFiles = new List<OutgoingFile>();
        string basePath;
        string fileToProcess;
        string transactionDir;
        string commitDir;

        class OutgoingFile
        {
            public string TxPath { get; private set; }
            public string TargetPath { get; private set; }

            public OutgoingFile(string txPath, string targetPath)
            {
                TxPath = txPath;
                TargetPath = targetPath;
            }
        }

    }


}