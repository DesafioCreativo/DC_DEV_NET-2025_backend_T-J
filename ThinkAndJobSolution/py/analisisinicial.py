import re
import sys

def extraer_nombres_cs(ruta_archivo):
    try:
        with open(ruta_archivo, 'r', encoding='utf-8') as f:
            contenido = f.read()
    except FileNotFoundError:
        print(f"Error: No se encontró el archivo '{ruta_archivo}'")
        return None
    except Exception as e:
        print(f"Error al leer el archivo: {e}")
        return None

    # Expresión regular para detectar ENDPOINTS (async Task<T>, JsonResult, IActionResult)
    patron_endpoint = re.compile(
        r'\bpublic\s+(?:async\s+Task<\w+>|JsonResult|IActionResult)\s+(\w+)\s*\(',
        re.MULTILINE
    )

    # Expresión regular para detectar ESTRUCTURAS (structs)
    patron_struct = re.compile(r'\bpublic\s+struct\s+(\w+)', re.MULTILINE)

    # Expresión regular para detectar MÉTODOS NORMALES (incluyendo private y static)
    patron_metodo = re.compile(
        r'\b(?:public|private)\s+(?:static\s+)?(?:\w+<\w+>|\w+)\s+(\w+)\s*\(',
        re.MULTILINE
    )

    # Extraer los endpoints (solo métodos con `async Task<T>`, `JsonResult` o `IActionResult`)
    endpoints = set(patron_endpoint.findall(contenido))

    # Extraer nombres de estructuras
    structs_nombres = sorted(set(patron_struct.findall(contenido)))

    # Extraer nombres de métodos, EXCLUYENDO los que ya son endpoints
    metodos_nombres = sorted(set(patron_metodo.findall(contenido)) - endpoints)

    # Ordenar los endpoints
    endpoints_nombres = sorted(endpoints)

    return {
        "Endpoints": endpoints_nombres,
        "Estructuras": structs_nombres,
        "Métodos": metodos_nombres
    }

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Uso: python script.py <ruta_del_archivo.cs>")
    else:
        ruta_cs = sys.argv[1]
        resultado = extraer_nombres_cs(ruta_cs)

        if resultado:
            print("\n--- Resultados ---\n")

            print("**Endpoints:**")
            if resultado["Endpoints"]:
                for ep in resultado["Endpoints"]:
                    print(f" - {ep}")
            else:
                print(" (No se encontraron endpoints)\n")

            print("\n**Estructuras:**")
            if resultado["Estructuras"]:
                for struct in resultado["Estructuras"]:
                    print(f" - {struct}")
            else:
                print(" (No se encontraron estructuras)\n")

            print("\n**Métodos:**")
            if resultado["Métodos"]:
                for metodo in resultado["Métodos"]:
                    print(f" - {metodo}")
            else:
                print(" (No se encontraron métodos)\n")
