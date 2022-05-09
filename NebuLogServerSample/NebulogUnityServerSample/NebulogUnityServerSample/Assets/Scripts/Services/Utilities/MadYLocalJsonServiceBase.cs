using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using NebulogUnityServer.DataModels;
using imady.Message;

namespace NebulogUnityServer.Services
{
    /// <summary>
    /// 基于Unity streamingAssets文件实现的本地数据服务
    /// </summary>
    /// <typeparam name="TClass">要记录的实体信息类型</typeparam>
    /// <typeparam name="TIndexType">用于TClass实现索引的变量类型</typeparam>
    public abstract class MadYLocalJsonServiceBase<TClass, TIndexType> where TClass : IMadYServiceIndexable<TIndexType>
    {
        private List<TClass> Repo;
        protected List<TClass> GetAll() => Repo;

        private string FileName;

        /// <summary>
        /// import all records from the repository file (usually under Assets/StreamingAssets folder).
        /// </summary>
        /// <param name="fileName"></param>
        protected virtual MadYServiceMsg Init(string fileName)
        {
            Repo = new List<TClass>();
            FileName = fileName;

            //This will create a new respository file if not exist
            if (!File.Exists(fileName)) { UpdateRepository(Repo); }

            StreamReader streamreader = new StreamReader(FileName);
            var stream = streamreader.ReadToEnd();

            try
            {
                if (stream != null && stream.Length > 0)
                {
                    Repo.Clear();
                    Repo = JsonConvert.DeserializeObject<List<TClass>>(stream, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        Culture = new System.Globalization.CultureInfo("zh-CN")
                    });
                    //Debug.Log("[LocalJsonServiceBase]: The simulative user database was loaded successfully.");
                }
                streamreader.Close();
                return new MadYServiceMsg()
                {
                    msg = "[LocalJsonServiceBase]: The simulative repo was loaded successfully.",
                    success = true
                };
            }
            catch (Exception e)
            {
                return new MadYServiceMsg($"[LocalJsonServiceBase] Exception: {e.Message}");
            }
        }

        internal void CloseRepo()
        {
            Repo.Clear();
        }

        /// <summary>
        /// Add one user record to the repository.
        /// </summary>
        /// <param name="record"></param>
        protected virtual MadYServiceMsg Add(TClass record)
        {
            if (Repo != null)
            {
                Repo.Add(record);
                UpdateRepository(Repo);
                return new MadYServiceMsg(record);
            }
            else
                return new MadYServiceMsg("[LocalJsonServiceBase]:  Simulation Server Error - the designated repository was not initialized.");
        }

        /// <summary>
        /// !!!完全覆盖原用户！Overwrite the previous user repository!
        /// 注意，如果更新时没有包含原用户信息集合，而仅仅是新用户，则会造成原用户数据大量丢失。
        /// </summary>
        /// <param name="recordList"></param>
        private MadYServiceMsg UpdateRepository(List<TClass> recordList)
        {
            string jsonText = string.Empty;
            try
            {
                if (recordList != null && recordList.Count > 0)
                    jsonText = JsonConvert.SerializeObject(Repo.ToArray());
            }
            catch (Exception e)
            {
                return new MadYServiceMsg($"[LocalJsonServiceBase] JSON Exception: {e.Message}");
            }

            try
            {
                //Todo: better to modify the code to appending contents......
                if (File.Exists(FileName))
                {
                    FileInfo file = new FileInfo(FileName);
                    file.Delete();
                }
                //Write user information into repository file
                StreamWriter streamWriter = new StreamWriter(FileName, false);
                streamWriter.Write(jsonText);
                streamWriter.Close();
                return new MadYServiceMsg() { msg = $"[LocalJsonServiceBase]: {typeof(TClass)} 数据库已经更新。", success = true };
            }
            catch (Exception e)
            {
                return new MadYServiceMsg($"[LocalJsonServiceBase] Streaming Writing Exception: {e.Message}");
            }
        }

        /// <summary>
        /// Update one anchor record in the repository.
        /// </summary>
        /// <param name="record"></param>        
        protected virtual MadYServiceMsg Update(TClass record)
        {
            try
            {
                var query = Repo.Where(a => a.objectIndex.Equals(record.objectIndex)).FirstOrDefault();
                if (query != null)
                {
                    var index = Repo.FindIndex(a => a.objectIndex.Equals(record.objectIndex));
                    Repo[index] = record;
                }
                else
                    Add(record);
                UpdateRepository(Repo);
                return new MadYServiceMsg(record);
            }
            catch (Exception e)
            {
                return new MadYServiceMsg($"[LocalJsonServiceBase]: Record update error: {e.Message}");
            }
        }

        /// <summary>
        /// Delete the designated user record.    
        /// </summary>
        /// <param name="recordId">以GUID方式记录的User Id。</param>
        protected virtual MadYServiceMsg Delete(TIndexType recordId)
        {
            try
            {
                var item = Repo.Where(a => a.objectIndex.Equals(recordId)).FirstOrDefault();
                Repo.Remove(item);
                return new MadYServiceMsg() { success = true, msg = $"[LocalJsonServiceBase]: Record {recordId} Deleted." };
            }
            catch (Exception e)
            {
                //Debug.LogException(e);
                return new MadYServiceMsg($"[LocalJsonServiceBase]: Record update error: {e.Message}");
            }
        }

        protected virtual MadYServiceMsg Delete(TClass record)
        {
            try
            {
                //prevent the situation that two different records with identical ID exist.
                var presearch = Repo.Find(a => a.objectIndex.Equals(record.objectIndex));

                if (presearch != null && record.Equals(presearch))
                {
                    Delete(record.objectIndex);
                    return new MadYServiceMsg() { success = true, msg = $"[LocalJsonServiceBase]: Record {record.objectIndex} Deleted." };
                }
                else
                {
                    return new MadYServiceMsg($"[LocalJsonServiceBase]: The data to delete has variance to record in database. Aborted.");
                }
            }
            catch (Exception e)
            {
                return new MadYServiceMsg($"[LocalJsonServiceBase]: Record update error: {e.Message}");
            }
        }

        /// <summary>
        /// Get the User instance designated by UserId (GUID).
        /// </summary>
        /// <param name="recordId"></param>
        /// <returns></returns>
        protected virtual MadYServiceMsg Get(TIndexType recordId)
        {
            try
            {
                var instance = Repo.Where(a => a.objectIndex.Equals(recordId)).FirstOrDefault();
                if (instance == null)
                    return new MadYServiceMsg() { success = false, msgBody = default, msg = $"[LocalJsonServiceBase]: Record not found." };
                else
                    return new MadYServiceMsg(instance);
            }
            catch (Exception e)
            {
                return new MadYServiceMsg() { success = false, msgBody = default, msg = $"[LocalJsonServiceBase]: Record searching error: {e.Message}" };
            }
        }

    }
}