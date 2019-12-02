/*
 *  Liquid Fast Infoset - XML Compression Library
 *  Copyright © 2001-2011 Liquid Technologies Limited. All rights reserved.
 *  
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU Affero General Public License as
 *  published by the Free Software Foundation, either version 3 of the
 *  License, or (at your option) any later version.
 *  
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU Affero General Public License for more details.
 *  
 *  You should have received a copy of the GNU Affero General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *  
 *  For product and commercial licensing details please contact us:
 *  http://www.liquid-technologies.com
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace LiquidTechnologies.FastInfoset
{
	internal class FIWriterVocabulary
	{
		#region Inner Classes
		internal struct QNameIndex
		{
			internal void Init(string prefix, string ns, string localName)
			{
				index = -1;
				qname.Init(prefix, ns, localName);
			}

			internal QualifiedName qname;
			internal int index;
		}

		internal class QNameIndexLookup
		{
			internal QNameIndexLookup(QNameIndex qname)
			{
				_qnames = new QNameIndex[1];
				_qnames[0] = qname;
			}

			internal void AddQNameIndex(QNameIndex qname)
			{
				int len = _qnames.Length;
				QNameIndex[] buffer = new QNameIndex[len + 1];
				Array.Copy(_qnames, buffer, len);
				_qnames = buffer;
				_qnames[len] = qname;
			}

			internal bool TryGetIndex(string prefix, string ns, out int index)
			{
				for (int n = 0; n < _qnames.Length; n++)
				{
					QNameIndex qnameIndex = _qnames[n];
					if ((qnameIndex.qname.prefix == prefix) && (qnameIndex.qname.ns == ns))
					{
						index = qnameIndex.index;
						return true;
					}
				}

				index = -1;
				return false;
			}

			internal bool Contains(string prefix, string ns)
			{
				for (int n = 0; n < _qnames.Length; n++)
				{
					QNameIndex qnameIndex = _qnames[n];
					if ((qnameIndex.qname.prefix == prefix) && (qnameIndex.qname.ns == ns))
						return true;
				}

				return false;
			}

			private QNameIndex[] _qnames;
		}

		internal class QNameArray
		{
			internal QNameArray()
			{
				_lastIndex = 0;
				_nameQNameIndexLookupMap = new Dictionary<string, QNameIndexLookup>();
			}

			internal QNameArray(QNameArray qnameArray)
			{
				_lastIndex = qnameArray._lastIndex;
				_nameQNameIndexLookupMap = qnameArray._nameQNameIndexLookupMap;
			}

			internal bool TryAddQName(QNameIndex qnameIndex, out int index)
			{
				QNameIndexLookup qnameLookup;
				if (_nameQNameIndexLookupMap.TryGetValue(qnameIndex.qname.localName, out qnameLookup))
				{
					// found QNameIndexLookup for localName, so try match prfix and namespace
					if (qnameLookup.TryGetIndex(qnameIndex.qname.prefix, qnameIndex.qname.ns, out index))
						return false;

					// match not found, so add a new entry
					_lastIndex++;
					qnameIndex.index = _lastIndex;
					qnameLookup.AddQNameIndex(qnameIndex);
				}
				else
				{
					// match not found, so add a new lookup entry for localName
					_lastIndex++;
					qnameIndex.index = _lastIndex;
					_nameQNameIndexLookupMap.Add(qnameIndex.qname.localName, new QNameIndexLookup(qnameIndex));
				}

				index = -1;
				return true;
			}

			internal bool Contains(QNameIndex qnameIndex)
			{
				QNameIndexLookup qnameLookup;
				if (_nameQNameIndexLookupMap.TryGetValue(qnameIndex.qname.localName, out qnameLookup))
				{
					// found QNameIndexLookup
					return qnameLookup.Contains(qnameIndex.qname.prefix, qnameIndex.qname.ns);
				}

				return false;
			}

			private Dictionary<string, QNameIndexLookup> _nameQNameIndexLookupMap;
			private int _lastIndex;
		}
		#endregion

		#region enums
		internal enum StringEncoding
		{
			UTF8,
			UTF16BE
		}
		#endregion

		#region Constructors
		internal FIWriterVocabulary()
		{
			// internal vocabulary constructor
			_encodingAlgorithmManager = new FIEncodingAlgorithmManager();
			_restrictedAlphabetManager = new FIRestrictedAlphabetManager();

			Init();
		}

		internal FIWriterVocabulary(Uri uri, FIEncodingAlgorithmManager encodingAlgorithmManager, FIRestrictedAlphabetManager restrictedAlphabetManager)
		{
			if (uri != null)
				_uri = uri.ToString();

			// external vocabulary constructor
			_encodingAlgorithmManager = encodingAlgorithmManager;
			_restrictedAlphabetManager = restrictedAlphabetManager;

			Init();
		}

		internal FIWriterVocabulary(FIWriterVocabulary vocab)
		{
			// copy constructor
			_encodingAlgorithmManager = vocab._encodingAlgorithmManager;
			_restrictedAlphabetManager = vocab._restrictedAlphabetManager;

			_attributeNamesMap = new QNameArray(vocab._attributeNamesMap);
			_attributeValuesMap = new Dictionary<string,int>(vocab._attributeValuesMap);
			_elementNamesMap = new QNameArray(vocab._elementNamesMap);
			_contentCharacterChunksMap = new Dictionary<string,int>(vocab._contentCharacterChunksMap);
			_localNamesMap = new Dictionary<string,int>(vocab._localNamesMap);
			_namespaceNamesMap = new Dictionary<string,int>(vocab._namespaceNamesMap);
			_prefixNamesMap = new Dictionary<string,int>(vocab._prefixNamesMap);
			_otherNCNamesMap = new Dictionary<string, int>(vocab._otherNCNamesMap);
			_otherStringMap = new Dictionary<string, int>(vocab._otherStringMap);

			_uri = vocab._uri;
			_stringEncoding = vocab._stringEncoding;
		}

		private void Init()
		{
			_attributeNamesMap = new QNameArray();
			_attributeValuesMap = new Dictionary<string, int>();
			_elementNamesMap = new QNameArray();
			_contentCharacterChunksMap = new Dictionary<string, int>();
			_localNamesMap = new Dictionary<string, int>();
			_namespaceNamesMap = new Dictionary<string, int>();
			_prefixNamesMap = new Dictionary<string, int>();
			_otherNCNamesMap = new Dictionary<string, int>();
			_otherStringMap = new Dictionary<string, int>();

			// add default prefix and namespace
			_prefixNamesMap.Add(FIConsts.FI_DEFAULT_PREFIX, 1);
			_namespaceNamesMap.Add(FIConsts.FI_DEFAULT_NAMESPACE, 1);
		}
		#endregion

		#region Internal Interface
		internal QNameArray AttributeNamesMap { get { return _attributeNamesMap; } }
		internal Dictionary<string, int> AttributeValuesMap { get { return _attributeValuesMap; } }
		internal QNameArray ElementNamesMap { get { return _elementNamesMap; } }
		internal Dictionary<string, int> ContentCharacterChunksMap { get { return _contentCharacterChunksMap; } }
		internal Dictionary<string, int> LocalNamesMap { get { return _localNamesMap; } }
		internal Dictionary<string, int> NamespaceNamesMap { get { return _namespaceNamesMap; } }
		internal Dictionary<string, int> PrefixNamesMap { get { return _prefixNamesMap; } }
		internal Dictionary<string, int> OtherNCNamesMap { get { return _otherNCNamesMap; } }
		internal Dictionary<string, int> OtherStringMap { get { return _otherStringMap; } }

		#region Add Methods
		internal void AddAttribute(string prefix, string ns, string localName)
		{
			QNameIndex qname = new QNameIndex();
			qname.Init(prefix, ns, localName);
			AddQName(qname, _attributeNamesMap);
		}

		internal void AddElement(string prefix, string ns, string localName)
		{
			QNameIndex qname = new QNameIndex();
			qname.Init(prefix, ns, localName);
			AddQName(qname, _elementNamesMap);
		}

		internal void AddAttributeValue(string strValue)
		{
			AddValueToMap(strValue, _attributeValuesMap);
		}

		internal void AddContentCharacterChunk(string strValue)
		{
			AddValueToMap(strValue, _contentCharacterChunksMap);
		}

		internal void AddPrefixName(string name)
		{
			AddValueToMap(name, _prefixNamesMap);
		}

		internal void AddNamespaceName(string name)
		{
			AddValueToMap(name, _namespaceNamesMap);
		}

		internal void AddLocalName(string name)
		{
			AddValueToMap(name, _localNamesMap);
		}

		internal void AddOtherNCName(string otherNCName)
		{
			AddValueToMap(otherNCName, _otherNCNamesMap);
		}

		internal void AddOtherString(string otherString)
		{
			AddValueToMap(otherString, _otherStringMap);
		}

		internal void AddQName(QNameIndex qnameIndex, QNameArray mapQNames)
		{
			int index;
			if (mapQNames.TryAddQName(qnameIndex, out index))
			{
				// value was added

				int prefixIndex = 0;
				int namespaceIndex = 0;
				int localNameIndex = 0;

				if (!string.IsNullOrEmpty(qnameIndex.qname.prefix))
				{
					if (!FindPrefixNameIndex(qnameIndex.qname.prefix, out prefixIndex))
						AddPrefixName(qnameIndex.qname.prefix);
				}

				if (!string.IsNullOrEmpty(qnameIndex.qname.ns))
				{
					if (!FindNamespaceNameIndex(qnameIndex.qname.ns, out namespaceIndex))
						AddNamespaceName(qnameIndex.qname.ns);
				}

				if (!FindLocalNameIndex(qnameIndex.qname.localName, out localNameIndex))
					AddLocalName(qnameIndex.qname.localName);
			}
		}

		internal void AddValueToMap(string key, Dictionary<string, int> map)
		{
			if (map.Count < FIConsts.TWO_POWER_TWENTY)
				map.Add(key, map.Count + 1);
		}
		#endregion

		#region Lookup Methods
		internal string URI
		{
			get { return _uri; }
		}

		internal StringEncoding CharacterStringEncoding
		{
			get { return _stringEncoding; }
			set { _stringEncoding = value; }
		}

		internal FIRestrictedAlphabet RestrictedAlphabet(int fiTableIndex)
		{
			return _restrictedAlphabetManager.Alphabet(fiTableIndex);
		}

		internal FIEncoding EncodingAlgorithm(string uri)
		{
			return _encodingAlgorithmManager.Encoding(uri);
		}

		internal bool FindIndex(Dictionary<string, int> map, string key, ref int index)
		{
			if (map.ContainsKey(key))
			{
				index = map[key];
				return true;
			}

			return false;
		}

		internal bool FindPrefixNameIndex(string name, out int index)
		{
			return _prefixNamesMap.TryGetValue(name, out index);
		}

		internal bool FindNamespaceNameIndex(string name, out int index)
		{
			return _namespaceNamesMap.TryGetValue(name, out index);
		}

		internal bool FindLocalNameIndex(string name, out int index)
		{
			return _localNamesMap.TryGetValue(name, out index);
		}
		#endregion
		#endregion

		#region Members Variables
		// Internal Data
		private string _uri = null;
		private StringEncoding _stringEncoding = StringEncoding.UTF8;

		private FIEncodingAlgorithmManager _encodingAlgorithmManager;
		private FIRestrictedAlphabetManager _restrictedAlphabetManager;

		// Writer Tables
		private QNameArray _attributeNamesMap;
		private Dictionary<string, int> _attributeValuesMap;

		private QNameArray _elementNamesMap;
		private Dictionary<string, int> _contentCharacterChunksMap;

		private Dictionary<string, int> _localNamesMap;
		private Dictionary<string, int> _namespaceNamesMap;
		private Dictionary<string, int> _prefixNamesMap;

		private Dictionary<string, int> _otherNCNamesMap;
		private Dictionary<string, int> _otherStringMap;
		#endregion
	}
}
