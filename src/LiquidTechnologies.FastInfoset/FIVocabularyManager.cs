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
	/// <summary>
	/// Manages external vocabulary objects
	/// </summary>
	public class FIVocabularyManager
	{
		/// <summary>
		/// Create an instance of the VocabularyManager class.
		/// </summary>
		public FIVocabularyManager()
		{
			_uriToVocabularyMap = new Dictionary<string, FIExternalVocabulary>();
		}

		/// <summary>
		/// Adds a FIExternalVocabulary object to the manager.
		/// </summary>
		/// <param name="vocabulary">Object to add.</param>
		/// <exception cref="LtFastInfosetException">A vocabulary already exists for URI.</exception>
		public void AddVocabulary(FIExternalVocabulary vocabulary)
		{
			string uri = vocabulary.URI.ToString();
			if (_uriToVocabularyMap.ContainsKey(uri))
				throw new LtFastInfosetException("A vocabulary already exists for URI " + uri);

			_uriToVocabularyMap.Add(uri, vocabulary);
		}

		internal FIReaderVocabulary ReaderVocabulary(string uri)
		{
			FIExternalVocabulary vocab = null;
			if (_uriToVocabularyMap.TryGetValue(uri, out vocab))
				return vocab.Reader;

			return null;
		}

		internal FIWriterVocabulary WriterVocabulary(string uri)
		{
			FIExternalVocabulary vocab = null;
			if (_uriToVocabularyMap.TryGetValue(uri, out vocab))
				return vocab.Writer;

			return null;
		}

		private Dictionary<string, FIExternalVocabulary> _uriToVocabularyMap;
	}
}
