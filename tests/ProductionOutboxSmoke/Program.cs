using WoodFactoryErp.Desktop;

var file=Path.Combine(Path.GetTempPath(),"erp-outbox-test-"+Guid.NewGuid()+".sqlite");
void Check(bool condition,string message) {if(!condition)throw new Exception(message);}
try {
    string id;
    using(var outbox=new ProductionOutbox(file)) {
        id=outbox.Enqueue("alice","job","fingerprint",key=>"{\"operationId\":\""+key+"\",\"notes\":\"رد خامة\"}");
        Check(outbox.Enqueue("alice","job","fingerprint",_=>throw new Exception("must reuse original"))==id,"Pending retry must reuse operation ID");
        Check(outbox.Commands("alice").Single().Payload.Contains("رد خامة"),"Arabic payload must round-trip");
        Check(outbox.Commands("bob").Count==0,"Users must not see another user's queue");
        outbox.Cache("alice:job","{\"status\":\"in_progress\"}");
        try {outbox.Enqueue("alice","job","failure",_=>throw new InvalidOperationException("simulated failure"));}catch(InvalidOperationException){}
        Check(outbox.Commands("alice").Count==1,"Failed local save must leave no orphan outbox/draft");
    }
    using(var reopened=new ProductionOutbox(file)) {
        Check(reopened.Commands("alice").Single().Id==id,"Draft and operation ID must survive restart");
        Check(reopened.Cached("alice:job")!.Contains("in_progress"),"Offline cache must survive restart");
        reopened.Mark(id,"blocked","Insufficient stock");
        Check(reopened.Commands("alice").Single().Detail=="Insufficient stock","Rejected reason retained");
        reopened.Mark(id,"pending","");
        Check(reopened.Commands("alice").Single().Id==id,"Manual retry preserves stable identity");
        reopened.Mark(id,"accepted","{\"alreadyPosted\":true}");
        Check(reopened.Command("alice",id)?.Status=="accepted","Inline feedback can read the durable acknowledgment");
        Check(reopened.Command("bob",id) is null,"Acknowledgment lookup is account scoped");
        Check(reopened.Commands("alice").Count==0,"Acknowledgment removes only queue visibility");
        var next=reopened.Enqueue("alice","job","fingerprint",key=>key);
        Check(next!=id,"A later intentional identical posting has its own operation ID");
        reopened.Mark(next,"blocked","Bad quantity");reopened.Mark(next,"abandoned","User discarded rejected local draft");
        Check(reopened.Commands("alice").Count==0,"Discarded rejection stays outside pending queue");
    }
    Console.WriteLine("PASS: SQLite durability, atomic local save, identity-preserving replay, Arabic, account isolation, cache and rejection handling.");
} finally {foreach(var suffix in new[]{"","-wal","-shm"})if(File.Exists(file+suffix))File.Delete(file+suffix);}
