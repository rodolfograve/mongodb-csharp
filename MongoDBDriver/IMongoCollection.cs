using System;
using System.Collections.Generic;
using System.Text;

namespace MongoDB.Driver {
	public interface IMongoCollection {
		string Name { get; }
		string DbName { get; }
		string FullName { get; }
		CollectionMetaData MetaData { get; }

		Document FindOne(Document spec);
        Document FindOne(object spec);

		ICursor FindAll();
		ICursor Find(String where);

        ICursor Find(Document spec);
        ICursor Find(object spec);
		
        ICursor Find(Document spec, int limit, int skip);
        ICursor Find(object spec, int limit, int skip);

        ICursor Find(Document spec, int limit, int skip, Document fields);
        ICursor Find(object spec, int limit, int skip, object fields);

		long Count();
        long Count(Document spec);
        long Count(object spec);

        void Insert(Document doc);
        void Insert(object doc);
        
        void Insert(IEnumerable<Document> docs);

        void Delete(Document selector);
        void Delete(object selector);

        void Update(Document doc);
        void Update(object doc);

        void Update(Document doc, Document selector);
        void Update(object doc, object selector);

        void Update(Document doc, Document selector, int upsert);
        void Update(object doc, object selector, int upsert);

        void UpdateAll(Document doc, Document selector);
        void UpdateAll(object doc, object selector);

	}
}
