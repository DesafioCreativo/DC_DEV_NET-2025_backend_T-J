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

    # Expresión regular para detectar ENDPOINTS (prioridad en la detección de métodos)
    patron_endpoint = re.compile(
        # r'\bpublic\s+(?:async\s+Task<\w+>|JsonResult|IActionResult)\s+(\w+)\s*\(',
        r'\bpublic\s+(?:async\s+)?(?:Task<\w+>|JsonResult|IActionResult)\s+(\w+)\s*\(',
        re.MULTILINE
    )

    # Expresión regular para detectar RUTAS (`Route(...)`) justo antes de un endpoint
    patron_route = re.compile(
        # r'^\s*\[\s*Route\s*\(\s*(?:template:\s*)?[\'"]([^\'"]+)[\'"]\s*\)\]',
        r'^\s*\[\s*Route\s*\(\s*(?:template:\s*)?[\'"]([^\'"]+)[\'"]\s*\)\]',
        re.MULTILINE
    )

    # Expresión regular para detectar ESTRUCTURAS (structs)
    patron_struct = re.compile(r'\bpublic\s+struct\s+(\w+)', re.MULTILINE)

    # Expresión regular para detectar MÉTODOS NORMALES (incluyendo `private` y `static`)
    patron_metodo = re.compile(
        r'\b(?:public|private)\s+(?:static\s+)?(?:\w+<\w+>|\w+)\s+(\w+)\s*\(',
        re.MULTILINE
    )

    # Extraer nombres de endpoints
    endpoints_detectados = patron_endpoint.findall(contenido)
    # print(endpoints_detectados)
    
    # Extraer nombres de estructuras
    structs_nombres = sorted(set(patron_struct.findall(contenido)))

    # Extraer nombres de métodos, EXCLUYENDO los que ya son endpoints
    metodos_nombres = sorted(set(patron_metodo.findall(contenido)) - set(endpoints_detectados))

    # Buscar rutas en el archivo y asociarlas a los endpoints
    rutas_detectadas = patron_route.findall(contenido) #list(patron_route.finditer(contenido))
    # print(rutas_detectadas)

    # Verificamos si la cantidad de rutas y endpoints coinciden
    # print(len(rutas_detectadas))
    # print(len(endpoints_detectados))

    # for endpoint in endpoints_detectados:
    #     print(endpoint)

    if len(rutas_detectadas) != len(endpoints_detectados):
        print("Advertencia: No hay una correspondencia exacta entre rutas y endpoints.")
        return None
    
    #endpoints_finales_palabra = [(endpoints_detectados[i], filtrar_parametros(rutas_detectadas[i])) for i in range(len(endpoints_detectados))]
    # Asociamos las rutas con los endpoints basándonos en su posición
    endpoints_finales = list(zip(endpoints_detectados, rutas_detectadas,rutas_detectadas))

    # Convertir las tuplas en listas para poder modificarlas
    endpoints_modificados = [list(fila) for fila in endpoints_finales]

    # Modificar el tercer valor de cada fila
    for fila in endpoints_modificados:
        fila[2] = filtrar_parametros(fila[1])

    # Convertir de nuevo a tuplas si es necesario
    endpoints_finales = [tuple(fila) for fila in endpoints_modificados]
    # print(endpoints_finales)

    # Ordenamos alfabéticamente por nombre de endpoint
    endpoints_nombres = sorted(endpoints_finales, key=lambda x: x[0])
    
    return {
        "Endpoints": endpoints_nombres,
        "Estructuras": structs_nombres,
        "Métodos": metodos_nombres
    }

def filtrar_parametros(ruta):
        # Lista de parámetros que queremos extraer
        parametros_interes = ["securityToken", "clientToken", "candidateId"]
        valores_encontrados = [param for param in parametros_interes if param in ruta]
        return ", ".join(valores_encontrados) if valores_encontrados else "(Sin Parámetros)"


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
                for ep, ruta,clave in resultado["Endpoints"]:
                    print(f" - {ep.ljust(40)}-{clave.ljust(40)}-{ruta.ljust(100)}")
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
