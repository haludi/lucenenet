/* 
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.IO;
using Lucene.Net.Support;
using NUnit.Framework;

using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
using _TestUtil = Lucene.Net.Util._TestUtil;

namespace Lucene.Net.Store
{
	
	[TestFixture]
	public class TestDirectory:LuceneTestCase
	{
		
		[Test]
		public virtual void  TestDetectClose()
		{
			Directory dir = new RAMDirectory();
			dir.Close();

            Assert.Throws<AlreadyClosedException>(() => dir.CreateOutput("test", null), "did not hit expected exception");
			
			dir = FSDirectory.Open(new System.IO.DirectoryInfo(AppSettings.Get("tempDir", System.IO.Path.GetTempPath())));
			dir.Close();
			Assert.Throws<AlreadyClosedException>(() => dir.CreateOutput("test", null), "did not hit expected exception");
		}
		
		
		// Test that different instances of FSDirectory can coexist on the same
		// path, can read, write, and lock files.
		[Test]
		public virtual void  TestDirectInstantiation()
		{
			System.IO.DirectoryInfo path = new System.IO.DirectoryInfo(AppSettings.Get("tempDir", System.IO.Path.GetTempPath()));
			
			int sz = 2;
			Directory[] dirs = new Directory[sz];
			
			dirs[0] = new SimpleFSDirectory(path, null);
			// dirs[1] = new NIOFSDirectory(path, null);
            System.Console.WriteLine("Skipping NIOFSDirectory() test under Lucene.Net");
			dirs[1] = new MMapDirectory(path, null);
			
			for (int i = 0; i < sz; i++)
			{
				Directory dir = dirs[i];
				dir.EnsureOpen();
				System.String fname = "foo." + i;
				System.String lockname = "foo" + i + ".lck";
				IndexOutput out_Renamed = dir.CreateOutput(fname, null);
				out_Renamed.WriteByte((byte) i);
				out_Renamed.Close();
				
				for (int j = 0; j < sz; j++)
				{
					Directory d2 = dirs[j];
					d2.EnsureOpen();
					Assert.IsTrue(d2.FileExists(fname, null));
					Assert.AreEqual(1, d2.FileLength(fname, null));
					
					// don't test read on MMapDirectory, since it can't really be
					// closed and will cause a failure to delete the file.
					if (d2 is MMapDirectory)
						continue;
					
					IndexInput input = d2.OpenInput(fname, null);
					Assert.AreEqual((byte) i, input.ReadByte(null));
					input.Close();
				}
				
				// delete with a different dir
				dirs[(i + 1) % sz].DeleteFile(fname, null);
				
				for (int j = 0; j < sz; j++)
				{
					Directory d2 = dirs[j];
					Assert.IsFalse(d2.FileExists(fname, null));
				}
				
				Lock lock_Renamed = dir.MakeLock(lockname);
				Assert.IsTrue(lock_Renamed.Obtain());
				
				for (int j = 0; j < sz; j++)
				{
					Directory d2 = dirs[j];
					Lock lock2 = d2.MakeLock(lockname);
					try
					{
						Assert.IsFalse(lock2.Obtain(1));
					}
					catch (LockObtainFailedException)
					{
						// OK
					}
				}
				
				lock_Renamed.Release();
				
				// now lock with different dir
				lock_Renamed = dirs[(i + 1) % sz].MakeLock(lockname);
				Assert.IsTrue(lock_Renamed.Obtain());
				lock_Renamed.Release();
			}
			
			for (int i = 0; i < sz; i++)
			{
				Directory dir = dirs[i];
				dir.EnsureOpen();
				dir.Close();
                Assert.IsFalse(dir.isOpen_ForNUnit);
			}
		}
		
		// LUCENE-1464
		[Test]
		public virtual void  TestDontCreate()
		{
			System.IO.DirectoryInfo path = new System.IO.DirectoryInfo(System.IO.Path.Combine(AppSettings.Get("tempDir", Path.GetTempPath()), "doesnotexist"));
			try
			{
				bool tmpBool;
				if (System.IO.File.Exists(path.FullName))
					tmpBool = true;
				else
					tmpBool = System.IO.Directory.Exists(path.FullName);
				Assert.IsTrue(!tmpBool);
				Directory dir = new SimpleFSDirectory(path, null);
				bool tmpBool2;
				if (System.IO.File.Exists(path.FullName))
					tmpBool2 = true;
				else
					tmpBool2 = System.IO.Directory.Exists(path.FullName);
				Assert.IsTrue(!tmpBool2);
				dir.Close();
			}
			finally
			{
				_TestUtil.RmDir(path);
			}
		}
		
		// LUCENE-1468
		[Test]
		public virtual void  TestRAMDirectoryFilter()
		{
			CheckDirectoryFilter(new RAMDirectory());
		}
		
		// LUCENE-1468
		[Test]
		public virtual void  TestFSDirectoryFilter()
		{
			CheckDirectoryFilter(FSDirectory.Open(new System.IO.DirectoryInfo("test")));
		}
		
		// LUCENE-1468
		private void  CheckDirectoryFilter(Directory dir)
		{
			System.String name = "file";
			try
			{
				dir.CreateOutput(name, null).Close();
				Assert.IsTrue(dir.FileExists(name, null));
				Assert.IsTrue(new System.Collections.ArrayList(dir.ListAll(null)).Contains(name));
			}
			finally
			{
				dir.Close();
			}
		}
		
		// LUCENE-1468
		[Test]
		public virtual void  TestCopySubdir()
		{
			System.IO.DirectoryInfo path = new System.IO.DirectoryInfo(System.IO.Path.Combine(AppSettings.Get("tempDir", Path.GetTempPath()), "testsubdir"));
			try
			{
				System.IO.Directory.CreateDirectory(path.FullName);
				System.IO.Directory.CreateDirectory(new System.IO.DirectoryInfo(System.IO.Path.Combine(path.FullName, "subdir")).FullName);
				Directory fsDir = new SimpleFSDirectory(path, null);
				Assert.AreEqual(0, new RAMDirectory(fsDir, null).ListAll(null).Length);
			}
			finally
			{
				_TestUtil.RmDir(path);
			}
		}
		
		// LUCENE-1468
		[Test]
		public virtual void  TestNotDirectory()
		{
			System.IO.DirectoryInfo path = new System.IO.DirectoryInfo(System.IO.Path.Combine(AppSettings.Get("tempDir", Path.GetTempPath()), "testnotdir"));
			Directory fsDir = new SimpleFSDirectory(path, null);
			try
			{
				IndexOutput out_Renamed = fsDir.CreateOutput("afile", null);
				out_Renamed.Close();
				Assert.IsTrue(fsDir.FileExists("afile", null));

			    Assert.Throws<NoSuchDirectoryException>(
			        () =>
			        new SimpleFSDirectory(new System.IO.DirectoryInfo(System.IO.Path.Combine(path.FullName, "afile")), null),
			        "did not hit expected exception");
			}
			finally
			{
				fsDir.Close();
				_TestUtil.RmDir(path);
			}
		}
	}
}