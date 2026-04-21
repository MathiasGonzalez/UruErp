# Security Policy

## Versiones soportadas

UruErp está en desarrollo activo. Solo la rama `main` recibe parches de seguridad.

| Rama / versión | Soportada |
|----------------|:---------:|
| `main` | ✅ |
| ramas de feature | ❌ |

## Reportar una vulnerabilidad

Si encontrás una vulnerabilidad de seguridad en UruErp o en los componentes que integra (API .NET, Workers de Cloudflare, imágenes Docker), **no abras un issue público**.

Enviá un reporte privado a través de [GitHub Private Vulnerability Reporting](https://github.com/MathiasGonzalez/UruErp/security/advisories/new) incluyendo:

- Descripción del problema y componente afectado.
- Pasos para reproducirlo.
- Impacto potencial.

Recibirás una respuesta en un plazo de 72 horas hábiles. Si el reporte es aceptado, se publicará un advisory y se acreditará al descubridor (salvo preferencia de anonimato).

## Consideraciones de seguridad conocidas

- **Certificados DGI:** nunca incluyas el `.pfx` en la imagen Docker para ambientes de producción. Usá Railway Volumes.
- **Jwt\_\_Secret:** debe ser una cadena aleatoria de al menos 32 caracteres generada con `openssl rand -hex 32`.
- **CORS:** `AllowedOrigins` en la API debe apuntar exclusivamente al Worker `api-proxy`; nunca usar `*` en producción.
- **PgBouncer:** corre en modo `transaction`, adecuado para conexiones de corta duración. El TLS entre PgBouncer y PostgreSQL está habilitado en producción (`SERVER_TLS_SSLMODE=require`).
