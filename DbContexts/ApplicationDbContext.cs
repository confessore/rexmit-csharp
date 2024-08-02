using Microsoft.EntityFrameworkCore;

namespace rexmit.DbContexts;

public class DefaultDbContext(DbContextOptions<DefaultDbContext> options) : DbContext(options)
{

}