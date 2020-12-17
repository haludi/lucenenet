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
using Lucene.Net.Util;

namespace Lucene.Net.Search
{
	
	/// <summary> Expert: Collects sorted results from Searchable's and collates them.
	/// The elements put into this queue must be of type FieldDoc.
	/// 
	/// <p/>Created: Feb 11, 2004 2:04:21 PM
	/// 
	/// </summary>
	/// <since>   lucene 1.4
	/// </since>
	class FieldDocSortedHitQueue : PriorityQueue<FieldDoc>
	{
		internal ArraySegment<SortField> fields;

		// used in the case where the fields are sorted by locale
		// based strings
		internal volatile System.Globalization.CompareInfo[] collators;
		
		
		/// <summary> Creates a hit queue sorted by the given list of fields.</summary>
		/// <param name="size">The number of hits to retain.  Must be greater than zero.</param>
		internal FieldDocSortedHitQueue(int size)
		{
			Initialize(size);
		}
		
		
		/// <summary> Allows redefinition of sort fields if they are <c>null</c>.
		/// This is to handle the case using ParallelMultiSearcher where the
		/// original list contains AUTO and we don't know the actual sort
		/// type until the values come back.  The fields can only be set once.
		/// This method is thread safe.
		/// </summary>
		/// <param name="fields"></param>
		internal virtual void  SetFields(ArraySegment<SortField> fields)
		{
            lock (this)
			{
				this.fields = fields;
				this.collators = HasCollators(fields);
			}
		}

        /// <summary>Returns the fields being used to sort. </summary>
        internal virtual ArraySegment<SortField> GetFields()
        {
            return fields;
        }


		/// <summary>Returns an array of collators, possibly <c>null</c>.  The collators
		/// correspond to any SortFields which were given a specific locale.
		/// </summary>
		/// <param name="fields">Array of sort fields.</param>
		/// <returns> Array, possibly <c>null</c>.</returns>
		private System.Globalization.CompareInfo[] HasCollators(ArraySegment<SortField> fields)
		{
			if (fields == null)
				return null;
			System.Globalization.CompareInfo[] ret = new System.Globalization.CompareInfo[fields.Count];
			for (int i = 0; i < fields.Count; ++i)
			{
				System.Globalization.CultureInfo locale = fields.Array[i + fields.Offset].Locale;
				if (locale != null)
					ret[i] = locale.CompareInfo;
			}
			return ret;
		}
		
		
		/// <summary> Returns whether <c>a</c> is less relevant than <c>b</c>.</summary>
        /// <param name="docA">ScoreDoc</param>
        /// <param name="docB">ScoreDoc</param>
		/// <returns><c>true</c> if document <c>a</c> should be sorted after document <c>b</c>.</returns>
        public override bool LessThan(FieldDoc docA, FieldDoc docB)
		{
			int n = fields.Count;
			int c = 0;
			for (int i = 0; i < n && c == 0; ++i)
			{
				int type = fields.Array[i + fields.Offset].Type;
				if(type == SortField.STRING)
				{
                    if (docA.fields[i] is UnmanagedStringArray.UnmanagedString us1 &&
                        docB.fields[i] is UnmanagedStringArray.UnmanagedString us2)
                    {
                        // null values need to be sorted first, because of how FieldCache.getStringIndex()
                        // works - in that routine, any documents without a value in the given field are
                        // put first.  If both are null, the next SortField is used
                        if (us1.IsNull)
                        {
                            c = (us2.IsNull) ? 0 : -1;
                        }
                        else if (us2.IsNull)
                        {
                            c = 1;
                        }
                        else if (fields.Array[i + fields.Offset].Locale == null)
                        {
                            c = us1.CompareTo(us2);
                        }
                        else
                        {
                            c = collators[i].Compare(us1.ToString(), us2.ToString());
                        }
                    }
                    else
                    {
                        var s1 = (string) docA.fields[i];
                        var s2 = (string) docB.fields[i];
                        // null values need to be sorted first, because of how FieldCache.getStringIndex()
                        // works - in that routine, any documents without a value in the given field are
                        // put first.  If both are null, the next SortField is used
                        if (s1 == null)
                        {
                            c = (s2 == null) ? 0 : -1;
                        }
                        else if (s2 == null)
                        {
                            c = 1;
                        }
                        else if (fields.Array[i + fields.Offset].Locale == null)
                        {
                            c = s1.CompareTo(s2);
                        }
                        else
                        {
                            c = collators[i].Compare(s1, s2);
                        }
                    }
				   
                }
                else
                {
                    c = docA.fields[i].CompareTo(docB.fields[i]);
                    if (type == SortField.SCORE)
                    {
                        c = -c;
                    }
                }
				if (fields.Array[i + fields.Offset].Reverse)
				{
					c = - c;
				}
			}
			
			// avoid random sort order that could lead to duplicates (bug #31241):
			if (c == 0)
				return docA.Doc > docB.Doc;
			
			return c > 0;
		}
	}
}