using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace dchTestBot
{
    public class MessageContext : DbContext
    {
        public MessageContext(DbContextOptions<MessageContext> options)
            : base(options)
        { }

        public DbSet<Message> Messages { get; set; }
    }

    public class Message
    {
        public int MessageId { get; set; }
        public DateTime Time { get; set; }
        public string Content { get; set; }
    }
}