using Microsoft.EntityFrameworkCore;

namespace ImportFotos
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Iniciando a migração de fotos...");
            
            using (var contextProjeto1 = new MeuDbContextProjeto1())
            using (var contextProjeto2 = new MeuDbContextProjeto2())
            {
                var fotosProjeto1 = await contextProjeto1.Usuarios
                    .Where(u => u.Foto != null)
                    .Include(u => u.Identificacao)
                    .Select(u => new { u.Identificacao.NumeroIdentificacao, u.Foto })
                    .ToListAsync();
                
                var identificacoesProjeto2 = await contextProjeto2.Usuarios
                    .Where(u => u.Foto == null)
                    .Include(u => u.Identificacao)
                    .Select(u => new { u.Id, u.Identificacao.NumeroIdentificacao })
                    .ToListAsync();
                
                var caminhoOrigem = "/caminho/para/fotos/projeto1";
                var caminhoDestino = "/caminho/para/fotos/projeto2/private/foto_usuarios";
                
                var migrador = new MigradorDeFotos(caminhoOrigem, caminhoDestino);
                await migrador.MigrarFotos(fotosProjeto1, identificacoesProjeto2, contextProjeto2);
            }

            Console.WriteLine("Migração de fotos concluída.");
        }
    }

    public class MigradorDeFotos
    {
        private readonly string _caminhoOrigem;
        private readonly string _caminhoDestino;

        public MigradorDeFotos(string caminhoOrigem, string caminhoDestino)
        {
            _caminhoOrigem = caminhoOrigem;
            _caminhoDestino = caminhoDestino;
        }

        public async Task MigrarFotos(
            List<dynamic> fotosProjeto1, 
            List<dynamic> identificacoesProjeto2, 
            MeuDbContextProjeto2 contextProjeto2)
        {
            foreach (var foto in fotosProjeto1)
            {
                var identificacaoProjeto1 = foto.Identificacao;
                
                var usuarioProjeto2 = identificacoesProjeto2.FirstOrDefault(u => u.Identificacao == identificacaoProjeto1);

                if (usuarioProjeto2 != null)
                {
                    var usuarioId = usuarioProjeto2.Id;
                    var arquivoOrigem = Path.Combine(_caminhoOrigem, foto.Foto);

                    if (File.Exists(arquivoOrigem))
                    {
                        var novoNomeArquivo = $"{Guid.NewGuid()}_{identificacaoProjeto1}.jpg";
                        var arquivoDestino = Path.Combine(_caminhoDestino, novoNomeArquivo);

                        try
                        {
                            await Task.Run(() => File.Copy(arquivoOrigem, arquivoDestino, true));
                            Console.WriteLine($"Foto do usuário {usuarioId} copiada para {arquivoDestino}");
                            
                            await AtualizarFotoNoBancoDeDados(usuarioId, novoNomeArquivo, contextProjeto2);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao copiar a foto do usuário {usuarioId}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Foto com identificação {identificacaoProjeto1} não encontrada no projeto 1.");
                    }
                }
            }
        }

        private async Task AtualizarFotoNoBancoDeDados(int usuarioId, string novoNomeArquivo, MeuDbContextProjeto2 contextProjeto2)
        {
            var usuario = await contextProjeto2.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
            if (usuario != null)
            {
                usuario.Foto = novoNomeArquivo;
                await contextProjeto2.SaveChangesAsync();
                Console.WriteLine($"Foto do usuário {usuarioId} atualizada no banco de dados do projeto 2.");
            }
        }
    }
    
    public class MeuDbContextProjeto1 : DbContext
    {
        public DbSet<Usuario> Usuarios { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseMySql("Server=SEU_SERVIDOR1;Database=SEU_BANCO1;User Id=SEU_USUARIO1;Password=SUA_SENHA1;",
                new MySqlServerVersion(new Version(8, 0, 21)));
        }
    }
    
    public class MeuDbContextProjeto2 : DbContext
    {
        public DbSet<Usuario> Usuarios { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseMySql("Server=SEU_SERVIDOR2;Database=SEU_BANCO2;User Id=SEU_USUARIO2;Password=SUA_SENHA2;",
                new MySqlServerVersion(new Version(8, 0, 21)));
        }
    }
    
    public class Usuario
    {
        public int Id { get; set; }
        public int IdentificacaoId { get; set; }
        public string Foto { get; set; }
        
        public Identificacao Identificacao { get; set; }
    }
    
    public class Identificacao
    {
        public int Id { get; set; }
        public string NumeroIdentificacao { get; set; }
        public int UsuarioId { get; set; }
        
        public Usuario Usuario { get; set; } 
    }
}
