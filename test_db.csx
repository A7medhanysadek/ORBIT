using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.Models;

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer("Server=db60551.public.databaseasp.net; Database=db60551; User Id=db60551; Password=Db3_7C!dk+5J; Encrypt=False; MultipleActiveResultSets=True;")
    .Options;

using var db = new AppDbContext(options);
var streams = db.LiveStreams.ToList();
Console.WriteLine("Total streams: " + streams.Count);
foreach(var s in streams) {
    Console.WriteLine("Stream " + s.Id + " IsLive: " + s.IsLive + " EndedAt: " + s.EndedAt);
}
