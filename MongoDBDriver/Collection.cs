using System;
using System.Collections.Generic;
using System.IO;

using MongoDB.Driver.Bson;
using MongoDB.Driver.IO;
using System.Reflection;

namespace MongoDB.Driver
{
    public class Collection : IMongoCollection 
    {
        public enum UpdateFlags:int
        {
            Upsert = 0,
            MultiUpdate = 1
        }
        
        private Connection connection;
        
        private string name;        
        public string Name {
            get { return name; }
        }
        
        private string dbName;      
        public string DbName {
            get { return dbName; }
        }
        
        public string FullName{
            get{ return dbName + "." + name;}
        }
        
        private CollectionMetaData metaData;        
        public CollectionMetaData MetaData {
            get { 
                if(metaData == null){
                    metaData = new CollectionMetaData(this.dbName,this.name, this.connection);
                }
                return metaData;
            }
        }
        
        private static OidGenerator oidGenerator = new OidGenerator();
                
        public Collection(string name, Connection conn, string dbName)
        {
            this.name = name;
            this.connection = conn;
            this.dbName = dbName;
        }
                
        public Document FindOne(Document spec){
            ICursor cur = this.Find(spec, -1, 0, null);
            foreach(Document doc in cur.Documents){
                cur.Dispose();
                return doc;
            }
            //FIXME Decide if this should throw a not found exception instead of returning null.
            return null; //this.Find(spec, -1, 0, null)[0];
        }

        /// <summary>
        /// Same as FindOne but builds the document specification from the <paramref name="spec"/> object.
        /// You can use anonymous types to build the documents: <example>new { prop1 = 1, prop2 = "test" })</example>
        /// which resembles the MongoDB's native JavaScript syntax.
        /// </summary>
        public Document FindOne(object spec)
        {
            return this.FindOne(Document.BuildFromObject(spec));
        }
        
        public ICursor FindAll() {
            Document spec = new Document();
            return this.Find(spec, 0, 0, null);
        }
        
        public ICursor Find(String where){
            Document spec = new Document();
            spec.Append("$where", new Code(where));
            return this.Find(spec, 0, 0, null);
        }
        
        public ICursor Find(Document spec) {
            return this.Find(spec, 0, 0, null);
        }

        /// <summary>
        /// Same as Find but builds the document specification from the <paramref name="spec"/> object.
        /// You can use anonymous types to build the documents: <example>new { prop1 = 1, prop2 = "test" })</example>
        /// which resembles the MongoDB's native JavaScript syntax.
        /// </summary>
        public ICursor Find(object spec)
        {
            return Find(Document.BuildFromObject(spec));
        }

        public ICursor Find(Document spec, int limit, int skip)
        {
            return this.Find(spec, limit, skip, null);
        }

        /// <summary>
        /// Same as Find but builds the document specification from the <paramref name="spec"/> object.
        /// You can use anonymous types to build the documents: <example>new { prop1 = 1, prop2 = "test" })</example>
        /// which resembles the MongoDB's native JavaScript syntax.
        /// </summary>
        public ICursor Find(object spec, int limit, int skip)
        {
            return this.Find(Document.BuildFromObject(spec), limit, skip, null);
        }

        public ICursor Find(Document spec, int limit, int skip, Document fields)
        {
            if(spec == null) spec = new Document();
            Cursor cur = new Cursor(connection, this.FullName, spec, limit, skip, fields);
            return cur;
        }

        /// <summary>
        /// Same as Find but builds the document specification from the <paramref name="spec"/> object.
        /// You can use anonymous types to build the documents: <example>new { prop1 = 1, prop2 = "test" })</example>
        /// which resembles the MongoDB's native JavaScript syntax.
        /// </summary>
        public ICursor Find(object spec, int limit, int skip, object fields)
        {
            return this.Find(spec == null ? null : Document.BuildFromObject(spec), limit, skip, Document.BuildFromObject(fields));
        }

        public long Count()
        {
            return this.Count(new Document());
        }
        
        public long Count(Document spec){
            Database db = new Database(this.connection, this.dbName);
            try{
                Document ret = db.SendCommand(new Document().Append("count",this.Name).Append("query",spec));
                double n = (double)ret["n"];
                return Convert.ToInt64(n);
            }catch(MongoCommandException){
                //FIXME This is an exception condition when the namespace is missing. -1 might be better here but the console returns 0.
                return 0;
            }
            
        }

        public long Count(object spec)
        {
            return this.Count(Document.BuildFromObject(spec));
        }

        public void Insert(Document doc){
            Document[] docs = new Document[]{doc,};
            this.Insert(docs);
        }

        /// <summary>
        /// Same as Insert but builds the document specification from the <paramref name="spec"/> object.
        /// You can use anonymous types to build the documents: <example>new { prop1 = 1, prop2 = "test" })</example>
        /// which resembles the MongoDB's native JavaScript syntax.
        /// </summary>
        public void Insert(object doc)
        {
            this.Insert(Document.BuildFromObject(doc));
        }
        
        public void Insert(IEnumerable<Document> docs){
            InsertMessage im = new InsertMessage();
            im.FullCollectionName = this.FullName;
            List<Document> idocs = new List<Document>();
            foreach(Document doc in docs){
                if(doc.Contains("_id") == false){
                    Oid _id = oidGenerator.Generate();
                    doc.Prepend("_id",_id);
                }
            }
            idocs.AddRange(docs);
            im.Documents = idocs.ToArray();
            try{
                this.connection.SendMessage(im);    
            }catch(IOException ioe){
                throw new MongoCommException("Could not insert document, communication failure", this.connection,ioe);
            }   
        }

        public void Delete(Document selector){
            DeleteMessage dm = new DeleteMessage();
            dm.FullCollectionName = this.FullName;
            dm.Selector = selector;
            try{
                this.connection.SendMessage(dm);
            }catch(IOException ioe){
                throw new MongoCommException("Could not delete document, communication failure", this.connection,ioe);
            }
        }

        /// <summary>
        /// Same as Delete but builds the document specification from the <paramref name="spec"/> object.
        /// You can use anonymous types to build the documents: <example>new { prop1 = 1, prop2 = "test" })</example>
        /// which resembles the MongoDB's native JavaScript syntax.
        /// </summary>
        public void Delete(object selector)
        {
            this.Delete(Document.BuildFromObject(selector));
        }
        
        public void Update(Document doc){
            //Try to generate a selector using _id for an existing document.
            //otherwise just set the upsert flag to 1 to insert and send onward.
            Document selector = new Document();
            int upsert = 0;
            if(doc.Contains("_id")  & doc["_id"] != null){
                selector["_id"] = doc["_id"];   
            }else{
                //Likely a new document
                doc.Prepend("_id",oidGenerator.Generate());
                upsert = 1;
            }
            this.Update(doc, selector, upsert);
        }

        /// <summary>
        /// Same as Update but builds the document specification from the <paramref name="spec"/> object.
        /// You can use anonymous types to build the documents: <example>new { prop1 = 1, prop2 = "test" })</example>
        /// which resembles the MongoDB's native JavaScript syntax.
        /// </summary>
        public void Update(object doc)
        {
            this.Update(Document.BuildFromObject(doc));
        }
        
        public void Update(Document doc, Document selector){
            this.Update(doc, selector, 0);
        }

        /// <summary>
        /// Same as Update but builds the document specification from the <paramref name="spec"/> object.
        /// You can use anonymous types to build the documents: <example>new { prop1 = 1, prop2 = "test" })</example>
        /// which resembles the MongoDB's native JavaScript syntax.
        /// </summary>
        public void Update(object doc, object selector)
        {
            Update(Document.BuildFromObject(doc), Document.BuildFromObject(selector));
        }

        public void Update(Document doc, Document selector, UpdateFlags flags){
            UpdateMessage um = new UpdateMessage();
            um.FullCollectionName = this.FullName;
            um.Selector = selector;
            um.Document = doc;
            um.Flags = (int)flags;
            try{
                this.connection.SendMessage(um);
            }catch(IOException ioe){
                throw new MongoCommException("Could not update document, communication failure", this.connection,ioe);
            }           
            
        }

        /// <summary>
        /// Same as Update but builds the document specification from the <paramref name="spec"/> object.
        /// You can use anonymous types to build the documents: <example>new { prop1 = 1, prop2 = "test" })</example>
        /// which resembles the MongoDB's native JavaScript syntax.
        /// </summary>
        public void Update(object doc, object selector, UpdateFlags flags)
        {
            this.Update(Document.BuildFromObject(doc), Document.BuildFromObject(selector), flags);
        }
        
        public void Update(Document doc, Document selector, int flags){
            //TODO Update the interface and make a breaking change.
            this.Update(doc,selector,(UpdateFlags)flags);
        }

        /// <summary>
        /// Same as Update but builds the document specification from the <paramref name="spec"/> object.
        /// You can use anonymous types to build the documents: <example>new { prop1 = 1, prop2 = "test" })</example>
        /// which resembles the MongoDB's native JavaScript syntax.
        /// </summary>
        public void Update(object doc, object selector, int flags)
        {
            this.Update(Document.BuildFromObject(doc), Document.BuildFromObject(selector), flags);
        }
        
        /// <summary>
        /// Runs a multiple update query against the database.  It will wrap any 
        /// doc with $set if the passed in doc doesn't contain any '$' ops.
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="selector"></param>
        public void UpdateAll(Document doc, Document selector){
            bool foundOp = false;
            foreach(string key in doc.Keys){
                if(key.IndexOf('$') == 0){
                    foundOp = true;
                    break;
                }
            }
            if(foundOp == false){
                //wrap document in a $set.
                Document s = new Document().Append("$set", doc);
                doc = s;
            }
            this.Update(doc, selector, UpdateFlags.MultiUpdate);
        }

        /// <summary>
        /// Same as UpdateAll but builds the document specification from the <paramref name="spec"/> object.
        /// You can use anonymous types to build the documents: <example>new { prop1 = 1, prop2 = "test" })</example>
        /// which resembles the MongoDB's native JavaScript syntax.
        /// </summary>
        public void UpdateAll(object doc, object selector)
        {
            this.UpdateAll(Document.BuildFromObject(doc), Document.BuildFromObject(selector));
        }

    }
}