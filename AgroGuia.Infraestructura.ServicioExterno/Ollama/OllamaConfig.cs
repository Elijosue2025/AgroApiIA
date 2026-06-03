using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroGuia.Infraestructura.ServicioExterno.Ollama
{
    public class OllamaConfig
    {
        public string ModelName { get; set; } = "llama3.2:3b";
        public string BaseUrl { get; set; } = "http://localhost:11434";
    }
}