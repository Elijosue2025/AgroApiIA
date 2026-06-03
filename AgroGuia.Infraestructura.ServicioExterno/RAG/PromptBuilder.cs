namespace AgroGuia.Infraestructura.ServicioExterno.RAG
{
    public class PromptBuilder
    {
        public string ConstruirPromptRAG(
            string preguntaUsuario,
            List<string> chunks)
        {
            string contexto =
                string.Join("\n-------------------\n", chunks);

            return $@"
CONTEXTO OFICIAL AGRONÓMICO:

{contexto}

PREGUNTA DEL AGRICULTOR:
{preguntaUsuario}

INSTRUCCIONES:
- Responde únicamente usando el contexto oficial.
- No inventes información.
- Usa lenguaje sencillo y práctico.
- Si no existe información suficiente responde:
'La información no se encuentra disponible en las guías técnicas oficiales.'
";
        }
    }
}