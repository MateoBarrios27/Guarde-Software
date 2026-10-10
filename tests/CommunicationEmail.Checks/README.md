# Plantilla Logísticas e imágenes inline de Quill

Implementado en la copia de código del Escritorio el 2026-10-07. No hay cambios
de base de datos, destinatarios, credenciales SMTP o configuración del VPS.

## Uso

En Comunicaciones, elegir **Plantilla Logísticas**, revisar el asunto y seleccionar
los receptores del rubro correspondiente. **Editar texto con Quill** modifica
solamente la introducción, conservando el flyer y el documento de correo.
**Enviar Prueba** mantiene el flujo de prueba existente y usa el mismo armado
MIME que el envío normal. En esta tarea no se ejecutó un envío real.

El PNG aprobado se conserva sin modificaciones: SHA-256
`64C2397B5D7D68CB7259ED8A12DAE6E11CD34E92CB6E1A0D2C4A2D8D533AA340`.
La imagen se muestra a 560 px en escritorio y se adapta en móvil.
El pie sólo contiene «Abrir chat en WhatsApp» con el logo blanco inline; los
enlaces adicionales y la frase de baja se retiraron a pedido del usuario.
No es necesario cargar este flyer también como un adjunto normal.

## Despliegue

Actualizar tanto frontend como backend. El frontend incluye los dos archivos de
`src/assets/email-templates/logisticas/`. Publicar también
`GuardeSoftwareAPI/EmailTemplates/Logisticas/flyer_logisticas.png`: el glob
existente del csproj ya copia esta carpeta a build y publish.
Mantener las imágenes anteriores de `EmailTemplates/Inmobiliarias`.
No copiar sólo el DLL omitiendo los recursos de la nueva plantilla.
No se desplegó ni reinició producción durante esta tarea.

## Regresiones sin envíos

Desde la raíz del proyecto:

```powershell
dotnet run --project tests/CommunicationEmail.Checks/CommunicationEmail.Checks.csproj --disable-build-servers -p:UseSharedCompilation=false
```

Desde `GuardeSoftwareClient`, con Chrome instalado:

```powershell
$env:CHROME_BIN = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
node node_modules/@angular/cli/bin/ng.js test --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.communications.spec.json --include=src/app/shared/utils/communication-preview.util.spec.ts --include=src/app/shared/utils/logisticas-template.util.spec.ts --include=src/app/pages/communications/communications-logisticas.component.spec.ts
```

Las pruebas MIME verifican referencias CID, disposición inline, bytes idénticos,
deduplicación, límites y rechazo de fuentes inválidas; no usan SMTP ni SQL.
Las pruebas Angular usan servicios simulados, además del editor Quill real.
Los tests no implican que el correo haya sido recibido o visto en Gmail/Outlook.

## Verificación local

- Las comprobaciones MIME incluyen el flyer y el logo inline de WhatsApp y
  verifican que el pie no tenga los enlaces ni la frase retirados.
- 14 pruebas Angular aprobadas, incluida la edición mediante el Quill real.
- Backend compilado sin errores con salida aislada.
- Frontend compilado en producción sin errores con salida aislada.
- Vista revisada en 800 px y 390 px: imagen de 560 px y 326 px respectivamente,
  sin desbordamiento horizontal. Capturas en `tmp/communication-email-validation/`.
- Compilaciones con advertencias del proyecto existente: nulabilidad, presupuestos
  CSS/bundle, CommonJS y aviso NU1902 para MailKit 4.14.1. No se actualizó MailKit
  como parte de esta corrección; revisar esa dependencia por separado.

Antes de la campaña, enviar una prueba interna autorizada y revisar el mensaje
recibido en Gmail y el cliente de Windows. Los recursos inline no garantizan
idéntico renderizado ni evitan todas las políticas de bloqueo del destinatario.
