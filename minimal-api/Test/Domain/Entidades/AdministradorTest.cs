using MinimalApi.Dominio.Entidades;
namespace Test.Domain.Entidades;

[TestClass]
public class AdministradorTest
{
    [TestMethod]
    public void TestarGetSetPropriedades()
    {
      //Arrange - todas as variaveis para realizar as validações
      var adm = new Administrador();

      //Act - ações para executar / Propriedades
      adm.Id = 1;
      adm.Email = "teste@teste.com";
      adm.Senha = "teste";
      adm.Perfil = "Adm";

      //Assert - validação dos dados
      Assert.AreEqual(1, adm.Id);
      Assert.AreEqual("teste@teste.com", adm.Email);
      Assert.AreEqual("teste", adm.Senha);
      Assert.AreEqual("Adm", adm.Perfil);
    }
}