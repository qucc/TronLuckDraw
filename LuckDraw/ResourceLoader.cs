//-----------------------------------------------------------------------
// <copyright file="ResourceLoader.cs" company="troncell Tech">
//     Copyright © troncell Tech. All rights reserved.
// </copyright>
// <author>William Wu</author>
// <email>wulixu@troncelltech.com</email>
// <date>2012-10-24</date>
// <summary>no summary</summary>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Cache;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using LogService;

namespace SensingPlatform.Foundation.ResourceManager
{
    public class ResourceLoader : IDisposable
    {
        private static readonly IBizLogger logger = ServerLogFactory.GetLogger(typeof(ResourceLoader));

        private static ResourceLoader instance = new ResourceLoader();
        private Dictionary<Uri, WeakReference> resDic;
        private object locker = new object();
        private static readonly WeakReference NullWeakReference = new WeakReference(null);
        public static ResourceLoader Instance
        {
            get
            {
                return instance;
            }
        }
        private ResourceLoader()
        {

            resDic = new Dictionary<Uri, WeakReference>();
            autoEvent = new AutoResetEvent(true);
            waitQueue = new Queue<AsynLoadObj>();
            workThreadList = new List<Thread>();
            for (int index = 0; index < 5; index++)
            {
                Thread temp = new Thread(new ThreadStart(AsynchronousWorker));
                temp.Name = "Resource Loader Thread " + index;
                temp.Start();
                workThreadList.Add(temp);
            }
        }

        public void Dispose()
        {
            List<Thread>.Enumerator ie = workThreadList.GetEnumerator();
            while (ie.MoveNext())
            {
                ie.Current.Abort();
            }
            workThreadList.Clear();
            waitQueue.Clear();
            resDic.Clear();
        }

        /// <summary>
        /// Open Image File and unlock file
        /// Uri should be local uri
        /// </summary>
        /// <param name="pathUri"></param>
        /// <returns></returns>
        public BitmapImage OpenUnlockImage(Uri pathUri)
        {
            if (pathUri == null || (!pathUri.IsFile))
            {
                logger.Error("OpenResource Uri is null or Uri is not File");
                throw new UriFormatException("000006");
            }
            BitmapImage bi = new BitmapImage();
            try
            {

                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = new FileStream(pathUri.LocalPath, FileMode.Open);
                bi.EndInit();
                bi.ClearValue(BitmapImage.UriSourceProperty);
                bi.StreamSource.Dispose();
            }
            catch (Exception ex)
            {
                logger.Error("Open UnLock Image Error ", ex);
                logger.Error("OpenUnlockImage Uri Error");
                throw new UriFormatException("000006");
            }

            return bi;
        }


        /// <summary>
        /// Open Resource From WeakReference Dictionary
        /// </summary>
        /// <typeparam name="T">BitmapImage,MediaElement</typeparam>
        /// <param name="pathUri"></param>
        /// <returns></returns>
        /// <exception cref="CoreExcetpion"></exception>
        public T OpenResource<T>(Uri pathUri)
        // where T : DependencyObject
        {
            //Check Params
            if (pathUri == null || (!pathUri.IsFile))
            {
                logger.Error("OpenResource Uri is null or Uri is not File");
                throw new UriFormatException("000006");
            }
            logger.Debug("OpenResource ....pathUri " + pathUri);



            WeakReference wf;
            // WeakReference is null
            lock (locker)
            {
                if (!resDic.ContainsKey(pathUri))
                {
                    wf = InitWeakRefResource<T>(pathUri);
                    resDic.Add(pathUri, wf);
                }
                else
                {
                    if (!resDic[pathUri].IsAlive)
                    {
                        lock (locker)
                        {
                            if (!resDic[pathUri].IsAlive)
                            {
                                wf = InitWeakRefResource<T>(pathUri);
                                resDic[pathUri] = wf;
                            }
                        }
                    }

                }
            }
            return (T)(object)resDic[pathUri].Target;
        }



        private WeakReference InitWeakRefResource<T>(Uri pathUri)
        //   where T : DependencyObject
        {
            WeakReference result = NullWeakReference;
            result = new WeakReference(InitResource<T>(pathUri));
            return result;
        }

        private T InitResource<T>(Uri pathUri)
        //  where T : DependencyObject
        {
            T result;
            try
            {
                if (typeof(BitmapImage).Equals(typeof(T)))
                {
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = pathUri;
                    image.EndInit();
                    image.Freeze();
                    result = (T)(object)image;
                }
                else if (typeof(MediaElement).Equals(typeof(T)))
                {
                    MediaElement m = new MediaElement();
                    m.BeginInit();
                    m.Source = pathUri;
                    m.EndInit();
                    result = (T)(object)m;

                }
                else
                {
                    result = default(T);
                }
            }
            catch (Exception e)
            {
                UriFormatException ex = new UriFormatException("Load Resource Failed at ResourceLoader.", e);
                logger.Error(ex);
                throw ex;
            }
            return result;
        }


        #region AsynchronousLoadBitmapImage
        public delegate void AsynLoadCallBack(BitmapImage result);
        private class AsynLoadObj : IDisposable
        {
            public Uri ResourceUri;
            public Dispatcher dispatcher;
            public AsynLoadCallBack OnAsynLoadCallBack;

            public void Dispose()
            {
                OnAsynLoadCallBack = null;
                dispatcher = null;
            }
        }
        private List<Thread> workThreadList;
        private Queue<AsynLoadObj> waitQueue;
        private AutoResetEvent autoEvent;

        //public BitmapImage AsynOpenBitmapImage(Uri pathUri,Dispatcher dispatcher,AsynLoadCallBack callBack)
        public void AsynOpenBitmapImage(Uri pathUri, Dispatcher dispatcher, AsynLoadCallBack callBack)
        {
            if (resDic.ContainsKey(pathUri))
            {
                if (resDic[pathUri] != null && resDic[pathUri].Target != null)
                {
                    BitmapImage bitmap = resDic[pathUri].Target as BitmapImage;
                    if (bitmap == null)
                    {
                        logger.Error(string.Format("AsynOpenBitmapImage: {0} is cached, but the BitmapImage is null", pathUri));
                    }
                    callBack(bitmap);
                    //return bitmap;
                }
            }
            else
            {
                //TODO:
                resDic.Add(pathUri, null);
            }

            AsynLoadObj action = new AsynLoadObj();
            action.ResourceUri = pathUri;
            action.OnAsynLoadCallBack += callBack;
            action.dispatcher = dispatcher;

            lock (waitQueue)
            {
                waitQueue.Enqueue(action);
                //autoEvent.Set();
            }
            //return null;
        }

        private void AsynchronousWorker()
        {
            while (true)
            {
                if (waitQueue.Count == 0)
                {
                    Thread.Sleep(50);
                    //autoEvent.WaitOne();
                    continue;
                }

                AsynLoadObj target = null;
                lock (waitQueue)
                {
                    if (waitQueue.Count != 0)
                        target = waitQueue.Dequeue();
                }

                if (target == null)
                {
                    Thread.Sleep(5);
                    continue;
                }

                bool loadImageFinished = false;
                BitmapImage image = new BitmapImage();

                try
                {
                    
                    if (target.ResourceUri.Scheme.StartsWith("http"))
                    {
                        var webClient = new WebClient();
                        var buffer = webClient.DownloadData(target.ResourceUri);

                        using (var stream = new MemoryStream(buffer))
                        {
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.UriCachePolicy = new RequestCachePolicy(RequestCacheLevel.Default);
                            image.StreamSource = stream;
                            image.EndInit();
                        }

                    }
                    else
                    {
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.BeginInit();
                        image.UriSource = target.ResourceUri;
                        image.EndInit();
                        //image.Freeze();
                        //loadImageFinished = true;
                    }
                }
                catch (Exception ex)
                {
                    Uri picResource = target.ResourceUri;
                    if (picResource == null)
                    {
                        logger.Error("AsynOpenBitmapImage URI is NULL  ", ex);
                    }
                    else
                    {
                        UriFormatException error = new UriFormatException("AsynOpenBitmapImage Error", ex);
                        logger.Error("AsynOpenBitmapImage Error", error);
                    }
                    continue;
                }
                finally
                {
                    //image.DownloadCompleted -= image_DownloadCompleted;
                }

                if (!image.IsDownloading)
                {
                    if (image.CanFreeze)
                    {
                        image.Freeze();
                    }
                    WeakReference wr = new WeakReference(image, true);
                    resDic[target.ResourceUri] = wr;

                    if (target.OnAsynLoadCallBack != null)
                    {
                        logger.Debug("Load Asyn Image Finished " + target.ResourceUri.ToString());
                        target.dispatcher.BeginInvoke(target.OnAsynLoadCallBack, image);
                        target.Dispose();
                    }
                }
            }
        }

        void image_DownloadCompleted(object sender, EventArgs e)
        {
            var img = sender as BitmapImage;
            if (img != null)
            {
                img.Freeze();
                //loadImageFinished = true;
            }
        }
        #endregion
    }
}