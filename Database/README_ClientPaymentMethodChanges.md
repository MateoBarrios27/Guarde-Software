# Cambio de método de pago

Antes de desplegar, ejecutar `Update_Add_ClientPaymentMethodChanges.sql` en la base de la aplicación. Es idempotente y agrega únicamente la tabla de eventos y su índice. No cambia clientes ni débitos existentes. El script se verificó en una base temporal; no se ejecutó en la base de la aplicación.

## Operación

En **Editar cliente**, el medio actual se muestra bloqueado junto al botón **Cambiar método de pago**. Confirmar en el nuevo modal guarda únicamente esta operación; los demás campos de edición conservan su propio botón de guardado. Crear y reactivar clientes mantienen su flujo anterior.

El monto sugerido usa `payment_methods.commission`, administrado desde Settings:

`nuevo = actual / (1 + porcentajeActual / 100) * (1 + porcentajeNuevo / 100)`

Después de aplicar la equivalencia, se usa el mismo redondeo que en el cálculo de aumentos: si el nuevo método es efectivo, el importe sube al millar siguiente; para métodos bancarios, sube a la centena siguiente y las dos últimas cifras quedan en cero. Un porcentaje mayor produce un recargo; uno menor, un descuento relativo al monto actual. No se suman ni se restan directamente los puntos porcentuales sobre el abono. El resultado puede editarse antes de confirmar.

El servidor confirma en una sola transacción:

- Método activo seleccionado y abono vigente desde hoy.
- Ajuste del alquiler del último mes generado solo si queda deuda y tiene menos del 60 % cubierto. Exactamente 60 % conserva el débito.
- Cobertura calculada como `max(0, paid + advanced_payment - previous_balance - interests)` respecto de `monthly_debits`. Se reconstruyen los balances desde los movimientos antes y después de operar.
- Los créditos, intereses, meses anteriores y aumentos programados no se editan. Si no hay débito de alquiler, no se inventa uno; si hay varios en el mismo mes, se rechaza la operación para revisión.
- Registro independiente con fecha, nombres e importes anteriores y nuevos. Conserva sucesivos cambios en un mismo día y aparece como separador en el historial, con el tramo activo primero.

La solicitud incluye el método y monto que el usuario vio. Si cambiaron antes de confirmar, se rechaza para que vuelva a abrir el modal. La edición general también rechaza cambios directos del método para impedir que se omita la operación contable. Al perder la conexión no se guarda localmente ni se simula éxito: se debe volver a abrir el modal para comprobar el estado del servidor.

## Verificación

Desde la raíz del repositorio:

```powershell
dotnet run --project tests/PaymentMethodChange.Checks/PaymentMethodChange.Checks.csproj -p:BaseOutputPath=C:/Users/fsgbr/Documents/Guarde-Software/.codex_tmp/payment-method-tests/ -p:UseAppHost=false
```

La suite crea y elimina una base con nombre aleatorio en `.\SQLEXPRESS`; nunca toma la conexión de `appsettings.json`. Verifica límites de cobertura, adelantos, arrastre, intereses, historia, aumentos futuros, validaciones y rollback. Para otro servidor, agregar `-- "nombre-del-servidor"`.

También se verificaron compilación de frontend/backend y el nuevo modal en navegador aislado con servicios simulados: recargo, descuento, importe decimal editable, envío del monto elegido y ancho de escritorio/móvil. Falta ejecutar el script en la base de destino y comprobar el flujo autenticado completo sobre esa base.
