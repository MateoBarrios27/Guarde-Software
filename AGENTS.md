# Guarde-Software: instrucciones para agentes

## Copia de trabajo del sistema

La copia de trabajo autorizada del sistema es
`C:\Users\fsgbr\Desktop\GuardeSoftware\Guarde-Software`.
Usar esta carpeta para código, pruebas y preparación de builds. No tomar
`C:\Users\fsgbr\Documents\Guarde-Software` como una segunda copia del código:
contiene artefactos anteriores. Esta indicación no cambia rutas de producción,
servicios del VPS ni configuraciones SMTP.

## Comunicaciones: plantilla Logísticas e imágenes de Quill (2026-10-07)

La plantilla Logísticas usa un documento de correo con tablas, ancho máximo
de 600 px, flyer de 560 px y contactos HTML clickeables. La imagen aprobada se
distribuye en `GuardeSoftwareClient/src/assets/email-templates/logisticas/`
para vista previa y en `GuardeSoftwareAPI/EmailTemplates/Logisticas/` para envío.
El backend publica y copia estos recursos mediante el glob existente de
`EmailTemplates` en su csproj. No omitir la carpeta al desplegar.

El pie HTML de Logísticas termina con un botón verde «Abrir chat en WhatsApp»,
con el ícono blanco existente `cid:guarde-whatsapp` al lado del texto. Por pedido
del usuario no incluye debajo los enlaces de teléfono/web/Instagram ni la frase
de baja. El flyer aprobado se conserva sin cambios. Este acceso abre un chat
externo a pedido del lector; no habilita envíos automáticos por WhatsApp.

QuillModule permanece como editor general. En la plantilla Logísticas, «Editar
texto con Quill» modifica sólo el bloque entre los marcadores de copia; no pasar
el documento completo por Quill porque se perderían tablas, estilos y CID.
Cargar la plantilla no cambia destinatarios ni servidor SMTP, ni envía correos.
Logísticas/ecommerce no debe mezclarse con el texto de insumos médicos.

Las imágenes PNG/JPEG/GIF/WebP insertadas desde Quill como data URI se convierten
en recursos MIME inline únicamente al construir el mensaje de salida, mediante
`CommunicationEmailImages.Prepare`. El HTML enviado usa un CID coincidente con
el Content-ID del recurso y dimensiones explícitas adaptables. Los íconos y el
encabezado de las plantillas anteriores conservan sus dimensiones razonables.
No modificar el HTML persistido ni el historial auditado para hacer la conversión.
Pruebas, envíos normales y reintentos usan `CreateEmailMessage` del mismo job.
Los archivos adjuntados desde «Adjuntar Archivos» siguen siendo adjuntos normales.
Las imágenes remotas HTTP/HTTPS no se descargan desde el servidor; imágenes
locales, blob, CID sin recurso y formatos/data URI inválidos se rechazan con error.
Límites: 100 imágenes por correo, 10 MB por imagen incrustada y 20 MB de imágenes
incrustadas distintas. Repetir una imagen no duplica el recurso dentro del mensaje.

Las regresiones locales están en `tests/CommunicationEmail.Checks` y
`GuardeSoftwareClient/tsconfig.communications.spec.json`: comprueban MIME, bytes,
anchos, referencias inline, carga de plantilla, edición de texto y petición de
prueba con servicios simulados. No usan SMTP ni base de datos y no acreditan
recepción ni renderizado real en Gmail/Outlook. Antes de una campaña debe enviarse
una prueba interna autorizada y revisarse en esos clientes. No hay cambios de
esquema ni queries de migración para esta corrección.

## Presentación de fecha y hora en Finanzas

La columna Fecha del listado de Finanzas usa el mismo estilo compacto que
Activity Log: fecha `dd/MM/yyyy` arriba y hora `HH:mm` debajo. Ambas salen de
`paymentDate`. La fecha usa color `#334155`, tamaño `0.76rem` y peso `800`;
la hora usa `#94a3b8`, tamaño `0.69rem` y peso `700`, con separación de
`0.12rem` y altura de línea `1.18`. Conservar el ancho compacto y evitar saltos
dentro de cada línea. Este criterio es de presentación y no cambia la fecha
registrada, los cálculos, los filtros ni el ordenamiento de pagos.

## Reglas financieras críticas

Antes de modificar pagos, movimientos, saldos mensuales, intereses, fechas de
próximo pago, estados de cuenta o sincronización, leer:

- `Database/README_PaymentWaterfall.md`
- `tests/PaymentWaterfall.Checks/Program.cs`

Estas reglas son invariantes de negocio. No deben cambiarse como efecto
secundario de otra tarea. Si un requerimiento parece contradecirlas, explicar la
contradicción y pedir una decisión explícita antes de cambiar la regla.

### Cascada de cancelación de deuda

`PaymentAllocationEngine` es la fuente de verdad para imputar créditos. Cada
crédito se aplica cronológicamente en este orden:

1. Alquiler pendiente de períodos anteriores, del más antiguo al más reciente.
2. Intereses pendientes, del más antiguo al más reciente.
3. Alquiler del mes del crédito.
4. Alquileres de períodos futuros.
5. El remanente queda como saldo a favor.

No inferir componentes pagados restando intereses de `paid`. Usar
`allocated_rent`, `allocated_interests`, `unpaid_rent` y `unpaid_interests`.
Agregar un débito futuro no puede cambiar retroactivamente cómo se distribuyó
un pago anterior.

### Fecha de próximo pago

NextPaymentDay es el mes en que corresponde volver a cobrar o contactar.
Desde la consulta de pagos de 2026-10-09 tiene prioridad la última decisión
válida de cobranza guardada con el pago. Puede mantener un mes parcialmente
pagado o pasar al siguiente conservando el faltante como deuda anterior.
Consultar la sección "Consulta de meses parcialmente pagados y excedentes".

Sin decisión válida se conserva el cálculo histórico: máximo entre el mes
actual y el mes siguiente al último alquiler que recibió capital. Un alquiler
fue alcanzado si monthly_debits > 0 y unpaid_rent < monthly_debits (equivale a
allocated_rent > 0 tras reconstrucción). La fecha formal de payments no implica
cobertura. No usar sólo account_movements.payment_id: los débitos planificados
pueden tener payment_id NULL aunque luego reciban capital.

La decisión y los saldos deben coincidir en Clientes, detalle, Finanzas,
Dashboard, Caja, Comunicaciones y snapshot offline. No reemplazar la fecha elegida
por el mes actual ni avanzar automáticamente por cualquier pago parcial nuevo.

### Planificación de abonos, aumentos y promoción semestral

La planificación de pagos crea débitos mensuales futuros y luego reconstruye
`client_month_balances`; no debe existir un segundo mecanismo paralelo para
calcular esos meses.

- Para períodos menores a seis meses, si `increase_anchor_date` cae dentro del
  período, el operador debe poder informar el nuevo abono o elegir mantener el
  importe actual. Un aumento confirmado se aplica desde su mes a los meses
  posteriores del mismo plan.
- Para períodos de seis meses o más, el precio queda congelado automáticamente
  durante todo el período. No se aplican aumentos aunque
  `increase_anchor_date` caiga dentro de los meses planificados y no se debe
  permitir desactivar esta regla desde el frontend.
- Al confirmar un período congelado, `price_lock_end_date` e
  `increase_anchor_date` deben quedar en el mes siguiente al último débito del
  plan. No generar un mes adicional fuera de la cantidad elegida.
- Si el cliente tiene `is_six_month_promotion`, solamente el sexto débito del
  plan se cobra al 50% de forma predeterminada. El operador puede desactivar el
  beneficio para esa planificación y cobrar el sexto mes completo mediante
  `ChargeHalfSixthMonth = false`; no modificar por eso la condición permanente
  del cliente.
- El modal debe explicar antes de confirmar qué reglas se aplicarán, mostrar el
  importe de cada mes y distinguir las reglas automáticas de las decisiones
  manuales. El resumen del frontend y el resultado transaccional del backend
  deben coincidir.

Los casos de precio congelado, sexto mes al 50%, promoción desactivada y aumento
dentro de un período corto deben permanecer cubiertos por
`tests/PaymentWaterfall.Checks` y por la utilidad de desglose del frontend.

### Aumento omitido al planificar antes del débito automático

Cuando llega el débito automático de un mes y no se creó antes el débito
planificado de ese mes ni un tramo nuevo de importe, puede haber quedado sin
confirmar el aumento que vencía en ese período. Para este caso excepcional:

- Aplicar el aumento solamente si `increase_anchor_date` cae en el mes que se
  debita, el alquiler del mes anterior está totalmente impago
  (`monthly_debits > 0` y `unpaid_rent >= monthly_debits`), no hay débito ni
  tramo de `rental_amount_history` para el mes objetivo y no hay un bloqueo de
  precio vigente. La elegibilidad debe mantenerse alineada con el badge y la
  notificación `Aumento` de Clientes.
- Usar el porcentaje de `monthly_increase_settings` efectivo para el mes del
  débito. Calcularlo sobre el último importe vigente, redondear al millar más
  cercano como el job de aumentos existente, guardar un nuevo tramo de importe
  desde el primer día del mes y mover `increase_anchor_date` según la frecuencia
  del cliente (el mes del aumento cuenta como el primero del ciclo).
- Actualizar el tramo, el ancla, el débito automático y los saldos reconstruidos
  en la misma transacción. La comprobación de débito debe seguir usando
  `DaoAccountMovement.IsDebitAlreadyCreatedAsync` y su `sp_getapplock`.
- Si el mes no tiene porcentaje configurado, generar el débito con el importe
  vigente y conservar el ancla para revisión; no tomar el porcentaje de otro
  mes ni mover la próxima fecha de aumento.
- Un débito o tramo ya planificado impide esta excepción. Los bloqueos de precio
  de planes de seis meses o más prevalecen y nunca reciben este aumento durante
  el período congelado.

Cubrir con `tests/PaymentWaterfall.Checks` tanto el caso de alquiler anterior
totalmente impago como el de pago parcial, que debe conservar el importe actual.

### Estados de cuenta por Email y WhatsApp

Email y WhatsApp comparten la fuente financiera
`CommunicationDao.GetClientFinancialData(clientId, isNextMonth)`. No corregir
los importes solamente en una plantilla o canal: `PreviousBalance`, `Surcharge`
y `CurrentBalance` deben salir del mismo DTO para ambos.

`rentals.pending_surcharge` forma parte del recargo y del total a abonar mientras
todavía no fue materializado como movimiento. Debe incluirse en estados de
cuenta normales y proyectados, incluso cuando existen débitos planificados o
aumentos asignados. Cuando el recargo se convierte en un débito de
`account_movements`, el flujo normal vacía `pending_surcharge` dentro de la misma
transacción; desde ese momento el interés materializado se obtiene de
`client_month_balances`. No sumar simultáneamente la bolsa pendiente y el mismo
débito materializado.

Para un estado proyectado cuyo próximo pago vence en el mes objetivo, usar el
saldo acumulado correspondiente al último débito de alquiler planificado
relevante. No sumar filas acumulativas de `client_month_balances` entre sí y no
reducir el resultado al débito del primer mes: ambas opciones producen importes
incorrectos. La selección de un débito planificado debe exigir
`movement_type = 'DEBITO'` y un concepto de alquiler con `(Planificado)`; un
interés u otro débito futuro no puede tomarse como si fuera el último alquiler
del plan.

El identificador de pago se agrega una sola vez y únicamente cuando queda un
total positivo a abonar. Los cálculos de mes actual y mes proyectado deben usar
el calendario de Argentina y comparar períodos por año y mes.

#### Riesgo pendiente: movimientos fechados en el mes proyectado

Existe un comportamiento reportado en producción que todavía no está
reproducido de forma concluyente: estando, por ejemplo, en septiembre y
proyectando octubre, la existencia previa de un débito fechado en octubre podría
alterar el estado de cuenta. El caso observado pudo involucrar un débito de
interés por mora.

No asumir que todo débito del mes objetivo es alquiler ni aplicar una corrección
sin reproducir el caso. Antes de modificar la proyección:

1. Identificar el `rental_id` afectado y capturar, para el mes actual y el mes
   objetivo, sus `account_movements`, `client_month_balances` y los campos
   `pending_surcharge`, `pending_surcharge_period` y
   `pending_surcharge_rent_base`.
2. Determinar si el interés existe sólo como bolsa pendiente, sólo como débito
   materializado o incorrectamente en ambos lugares.
3. Reproducir en la base descartable al menos estos escenarios: interés futuro
   sin alquiler planificado; alquiler planificado más interés futuro; recargo
   pendiente aún no materializado; y recargo materializado con la bolsa ya en
   cero.
4. Verificar por separado `Surcharge` y `CurrentBalance` en el estado normal y
   en el proyectado. Un interés materializado debe aparecer una sola vez como
   recargo y una sola vez dentro del total; un recargo todavía pendiente debe
   seguir el mismo criterio sin desaparecer.
5. Confirmar que agregar el movimiento futuro no redistribuya retroactivamente
   pagos anteriores ni cambie `NextPaymentDay` salvo por una decisión explícita de
   cobranza o porque la cascada haya
   alcanzado alquiler de ese período.

Hasta contar con esa regresión, tratar este punto como un riesgo conocido y no
como una regla confirmada ni como un defecto ya corregido.

### Política temporal de comunicaciones: sólo Email

Mientras esta política esté activa, ninguna operación nueva debe enviar por
WhatsApp. La desactivación es transversal y no depende únicamente de ocultar un
control del frontend ni de `WAHASettings:Enabled`:

- El alta, edición, clonación y prueba de comunicados normalizan el canal a
  `Email`; la interfaz no ofrece un selector de canal.
- El backend vuelve a normalizar a `Email` toda petición de alta o edición para
  que una llamada directa a la API no pueda reactivar WhatsApp.
- El job de Quartz procesa únicamente el canal Email. En comunicados históricos
  mixtos omite WhatsApp; un comunicado histórico que sólo tenga WhatsApp no se
  puede enviar ni reintentar.
- Los reintentos seleccionan exclusivamente despachos fallidos de Email. La
  ampliación por rubro continúa disponible sólo para campañas exclusivamente de
  Email.
- Los recibos se configuran y entregan únicamente por Email. Aunque una petición
  antigua incluya `WhatsAppPhones`, el servicio no ejecuta ese envío y exige al
  menos un destinatario de Email.
- El acceso al panel de WAHA permanece oculto y `WAHASettings:Enabled` debe
  conservarse en `false` como defensa adicional.

No borrar los teléfonos, el indicador `phones.whatsapp`, los canales o los
despachos históricos: siguen siendo datos de contacto y auditoría. El detalle y
el historial pueden mostrar que una entrega anterior fue por WhatsApp, pero no
deben exponer acciones que la vuelvan a enviar. Para rehabilitar el canal se
deben revisar coordinadamente el formulario de Comunicaciones, la política del
backend, el job, los reintentos, la entrega de recibos, la configuración WAHA y
sus pruebas; cambiar sólo `appsettings` no es suficiente.

### Unicidad del débito mensual

La prevención de dos débitos de alquiler para el mismo alquiler y período es
una invariante técnica, no una decisión que deba trasladarse a un modal. El job
mensual, la planificación y la proyección al registrar un pago deben pasar por
`DaoAccountMovement.IsDebitAlreadyCreatedAsync`, que serializa la comprobación
y la posterior inserción mediante `sp_getapplock` con propietario
`Transaction`. No reemplazarla por una consulta `COUNT` sin bloqueo: dos
transacciones concurrentes podrían observar simultáneamente que el débito no
existe e insertar ambas.

### Consumidores que deben permanecer alineados

Al cambiar reglas financieras, revisar conjuntamente:

- `PaymentAllocationEngine` y `ClientMonthBalanceService`.
- Creación, edición y eliminación de pagos y movimientos.
- `DaoClient`, incluido `GetClientDetailByIdAsync`.
- `DaoRental` y `DaoCash`.
- Comunicaciones y estados de cuenta.
- `SyncService` y almacenamiento offline.
- Clientes, Finanzas y Dashboard en el frontend.

No reproducir manualmente la cascada desde el total `paid`. Los consumidores
deben leer la separación calculada por el motor común.

### Verificación obligatoria

Toda modificación de estas reglas debe actualizar y ejecutar
`tests/PaymentWaterfall.Checks`. Una compilación verifica código, pero no la
distribución contable ni la coherencia entre consumidores.

## Base de datos

Cuando una modificación requiera cambios de esquema o datos, dejar una query o
migración SQL idempotente dentro de `Database/` y documentar el orden de
aplicación. No desplegar código que lea columnas nuevas antes de aplicar su
migración.

## Procesos de desarrollo

Se puede compilar backend o frontend para verificar cambios, pero no dejar
procesos, watchers, servidores ni compilaciones ejecutándose al terminar.

## Diagnóstico de timeouts del listado de Clientes

El log analizado el 2026-10-06 muestra tres excepciones SQL de ejecución con
`Error Number: -2` al cargar el listado: `ClientController.GetClients` ->
`ClientService.GetClientsTableAsync` -> `DaoClient.GetTableClientsAsync` ->
`AccessDB.GetTableAsync`, durante `ExecuteReaderAsync`. Esto confirma que la
consulta no respondió dentro del tiempo permitido; no confirma si la causa fue
un bloqueo, un plan de ejecución lento, índices faltantes o carga del servidor.

La línea previa del comunicado 210 con estado `Finished` corresponde a otro
flujo. Indica que el job terminó sin errores registrados en su acumulador, pero
no acredita recepción de los correos ni demuestra que causó el timeout de
Clientes. Del comunicado 211 sólo se observa el inicio en ese fragmento.

Antes de corregir, medir la consulta con los filtros afectados, revisar sus
esperas/bloqueos y el plan de ejecución, y comprobar si los índices del script
`Database/Update_Add_ClientListIndexes.sql` están aplicados. La presencia del
archivo no confirma su instalación. No dar por resuelto el problema sólo por
aumentar el timeout, ni cambiar cálculos financieros basándose en este log.

## Navegación y edición de clientes

En la tabla de Clientes, el botón de edición general no se muestra: la edición
se abre desde «Editar cliente», junto a «Cerrar» en el encabezado de
`client-detail`. Al entrar a editar se cierra el detalle y se reutiliza la
ficha ya cargada para abrir el formulario en modo edición. La reactivación de
clientes dados de baja conserva su propio acceso y flujo. El rol Observador
puede abrir `client-detail`, pero no ve la acción de edición general.


## Fecha de retiro y proporcional del cliente

El flujo «Se va» / «Dar de baja» permite cualquier fecha válida de retiro,
incluido el mes actual y meses anteriores o futuros. El proporcional sugerido
se calcula con el abono correspondiente al mes elegido, desde su primer día
hasta el día de retiro inclusive, sobre la cantidad real de días de ese mes.
Se redondea al millar más cercano en efectivo y a la centena más cercana en
los demás medios, con mitades alejándose de cero. «Monto a cobrar» es editable:
el importe manual confirmado se guarda exactamente, con hasta dos decimales,
sin volver a aplicarle el redondeo sugerido. Se admite cero y se rechazan negativos.

El proporcional actualiza el único débito de alquiler del mes de retiro,
incluidos débitos planificados, conservando su identidad, fecha y asociación al
pago. No modifica los créditos registrados. Si no hay débito, crea uno fechado
el primer día de ese mes. Las confirmaciones repetidas no duplican el cargo.
Si hay más de un débito de alquiler para el período, se rechaza la operación
completa y se pide revisar los movimientos. La comprobación y escritura usan
el bloqueo transaccional común `IsDebitAlreadyCreatedAsync` / `sp_getapplock`.
Los saldos se reconstruyen con la cascada común dentro de la misma transacción.

«Quitar débito completo del mes siguiente» es una elección separada; seleccionar
proporcional de otro mes no elimina automáticamente ese débito. En «Se queda»,
la opción de deshacer usa el mes del último proporcional de salida; si no hay
proporcional mantiene el flujo del mes siguiente. Conserva la condición existente
de no generar un débito completo cuando hay deuda anterior. La opción de conservar
el proporcional no agrega un segundo alquiler encima.

La regresión se ejecuta con `tests/PaymentWaterfall.Checks`, en una base local
descartable, e incluye fechas pasadas y futuras, febrero bisiesto, redondeo por
medio, monto manual, repetición, restauración, créditos, duplicados y rollback.
Este cambio no requiere columnas nuevas ni una migración de datos existentes.
`Database/Check_ClientDepartureProportional.sql` contiene las consultas de
verificación para un alquiler concreto; no modifica datos.


## Movimientos para clientes dados de baja y saldo al reactivar

«Nuevo Movimiento» está disponible en `client-detail` también para clientes
dados de baja, salvo para Observador. La planificación de pagos sigue requiriendo
un cliente activo. Un movimiento manual usa el alquiler activo; si el cliente
está dado de baja, usa su último contrato cerrado, ordenado por inicio e ID.
No crea contratos ni reactiva al cliente. Sin contrato se rechaza la operación.
El movimiento y la reconstrucción de saldos son transaccionales.

La reactivación empieza con una confirmación simple cuyo botón dice «Empezar
reactivación». Luego consulta el saldo real del último contrato desde el ledger,
incluido el recargo pendiente. El segundo modal aparece sólo si ese saldo es
distinto de cero y permite «Mantener el saldo» o «Reactivar con saldo cero».
Después se abre el formulario compartido. Cancelar cualquiera de los pasos no
modifica datos: la decisión se aplica al guardar el formulario de reactivación.

La API valida que el cliente y su contrato anterior estén inactivos y compara
el contrato y saldo confirmados antes de crear el nuevo alquiler. Si el saldo
cambió mientras se completaba el formulario, exige volver a empezar para revisar
la decisión. El saldo visual usa negativo para deuda y positivo para saldo a favor.

Mantener el saldo registra un traspaso compensatorio que salda el contrato
cerrado y lleva capital, intereses y crédito remanente al nuevo contrato.
Los movimientos de apertura pertenecen al mes anterior a la reactivación para
que la deuda trasladada siga apareciendo como saldo anterior. Se usa la separación
de `PaymentAllocationEngine`, nunca se deducen los intereses desde el total pagado.
Saldo cero registra solamente el ajuste compensatorio del contrato anterior.
Si el saldo anterior es deuda, el ajuste es un CREDITO; si es saldo a favor,
es un DEBITO. El importe siempre es positivo y equivale al saldo neto cancelado.
El historial de movimientos del cliente incluye todos sus contratos, activos o
cerrados: debe mostrar el ajuste junto a los movimientos originales. Las pruebas
de reactivación verifican esta lectura, su conciliación y que el resumen y el
estado de cuenta excluyan la deuda e intereses cancelados al elegir saldo cero.
No se borran ni reescriben los débitos, créditos o pagos originales. Si hay recargo
pendiente, se materializa una sola vez antes del ajuste y se vacía su bolsa.
El saldo anterior elegido no incluye los nuevos cargos de alquiler de la
reactivación: éstos continúan generándose según el flujo existente.

Ambas elecciones reconstruyen los saldos y registran la decisión en la auditoría
dentro de la misma transacción que la reactivación. El contexto y la creación de
movimientos manuales bloquean la fila del cliente para evitar guardar sobre una
decisión financiera obsoleta. Las regresiones están en `ReactivationChecks.cs`,
ejecutadas por `tests/PaymentWaterfall.Checks`. No se agregan columnas ni se
requiere migrar datos: las consultas de verificación están en
`Database/Check_ClientReactivationBalance.sql`.


### Presentación del flujo de reactivación

La confirmación inicial y la elección del saldo usan `ClientReactivationModalComponent`,
con base blanca, encabezado amplio de azul sólido y texto blanco,
sin líneas decorativas celestes, bordes suaves y acciones anchas al pie. No se usan alertas SweetAlert para estas etapas.
«Confirmar y continuar» confirma la intención de reactivar y consulta la ficha y el saldo; el estado de carga bloquea
consultas duplicadas y cancelar destruye la consulta pendiente. El paso de saldo
aparece exclusivamente cuando el importe es distinto de cero y mantiene el saldo
como opción inicial. Deuda y saldo a favor se identifican por separado.
El primer modal es una confirmación concisa: título «Reactivar cliente»,
pregunta «¿Querés reactivar a este cliente?», nombre destacado y acciones «Cancelar» / «Confirmar y continuar».
No incluye frases promocionales, tarjetas de etapas ni explicaciones del proceso.
El modal de saldo conserva sus tarjetas de opciones y usa la misma paleta plana
de marca que la confirmación; mantiene visibles la deuda o crédito y las ayudas.
El formulario final recupera su estética original: encabezados, secciones,
campos y acciones vuelven a los estilos compartidos de `create-client-modal`.
El resumen de costos conserva el diseño nuevo y, por autorización posterior del
usuario, lo comparte con alta y edición mediante selectores `.cost-summary`.
También se unifica la presentación de agregar email y teléfono con `.contact-add`.
El resto del formulario conserva sus estilos previos. Las validaciones y errores se muestran dentro del formulario;
al guardar confirma con una notificación. Los modales permiten teclado, foco
contenido y Escape; el formulario impide cerrar durante el guardado.
El alta y la edición general mantienen su comportamiento.
La elección se aplica al guardar mediante la transacción existente; este cambio
de interfaz no altera el cálculo del saldo ni requiere queries o migraciones.
Como Clientes usa OnPush, los callbacks de carga y error del formulario de
reactivación deben marcar la vista con ChangeDetectorRef.markForCheck: los
estados y mensajes se actualizan sin esperar un segundo clic.


### Identidad visual de los dos modales previos a la reactivación

La dirección solicitada es de oversize moderado: jerarquías proporcionales,
contenido conciso y alto contraste, sin agrandar todo el modal.
Base blanca, encabezado sólido #2563EB con título blanco y nombre en #123A74.
No agregar líneas decorativas pequeñas celestes ni franjas de acento. El cierre
se centra verticalmente en el encabezado y su SVG dentro del área del botón.
Se usa Inter del proyecto; la confirmación mide hasta 600 px y el paso de saldo
hasta 640 px. Título de 26 px, nombre de 24 px e importe de hasta 34 px; botones
de 46 px de alto, con la acción principal proporcionalmente más ancha. Las
opciones de saldo alinean texto y radio arriba, sin espacio vacío deliberado.
No se usan textos promocionales, degradados, sombras ni desenfoque.
El fondo atenuado permite reconocer la pantalla de Clientes detrás del modal.

En el paso de saldo, el cliente aparece en el encabezado y el importe tiene
jerarquía propia. Las opciones se ubican lado a lado en escritorio y apiladas
en móvil; la seleccionada usa azul sólido, texto blanco y radio marcado.
Deuda, crédito y errores conservan etiquetas e iconos semánticos. Se mantienen
las aclaraciones sobre los nuevos cargos y la aplicación de la decisión al guardar.
El foco de teclado es visible; las acciones permanecen accesibles con contenido
largo mediante desplazamiento interno. Carga y errores se anuncian y muestran
dentro del modal. El diseño debe comprobarse con estados normal, carga, error,
saldo a favor/deuda, selección por teclado y nombres largos en pantallas chicas.

Estos estilos abarcan confirmación y saldo. En el formulario final de
`create-client-modal` el nuevo resumen de costos se comparte entre alta, edición
y reactivación, junto con las acciones de contacto; el resto mantiene su estética original. No cambian permisos, consultas,
cancelación, validaciones ni reglas financieras y no requieren migraciones.

### Resumen de costos y acciones de contacto del formulario compartido (2026-10-09)

El formulario final de reactivación conserva su estética anterior fuera del
resumen de costos. Por pedido posterior expreso del usuario, el resumen nuevo
se aplica también al alta y a la edición de clientes. Esta autorización amplía
el alcance visual previo; no rediseñar otras secciones del formulario.
El resumen usa azul sólido con texto blanco, importes agrupados y abono blanco
ancho y destacado, que sigue siendo editable. Conserva el proporcional y sus
controles, metros contratados y espacios ocupados. Los selectores `.cost-summary`
están limitados al componente compartido y no dependen de `isReactivation`.
Agregar email y agregar teléfono usan la misma clase `.contact-add`, icono plus,
altura, relleno verde, tipografía y estados de hover, foco y deshabilitado.
Los dos modales previos de confirmación y saldo mantienen su diseño. No se
modifican cálculos, validaciones, permisos, agregado de contactos ni persistencia.

## Consulta de meses parcialmente pagados y excedentes (2026-10-09)

La imputación contable sigue la cascada: capital anterior, intereses y alquileres.
La decisión del administrativo modifica el mes que se debe cobrar, sin perdonar
capital ni simular pagos adicionales.

- Un débito previamente generado que recibe capital y queda parcialmente pagado
  consulta si ese mes sigue pendiente o se toma como pagado para la cobranza.
  La primera opción mantiene ese mes como Próx. pago y no emite otro débito.
  La segunda pasa al mes siguiente, genera su débito si no existe y conserva
  lo que faltó pagar como Saldo anterior. Un débito ya planificado se reutiliza.
  No volver a consultar por capital de un mes anterior a una fecha ya elegida;
  un pago destinado sólo a esa deuda conserva el mes de cobranza todavía pendiente.
- Si se cancelan los débitos existentes y sobra dinero, se genera el próximo
  débito. Se consulta si ese mes con crédito sigue pendiente (credit_next_month)
  o se considera pagado para cobranza (close_credited_month). La primera opción
  conserva ese mes como Próx. pago; la segunda agrega un único débito adicional
  y lo fija como Próx. pago. El capital no cubierto del mes intermedio queda
  como Saldo anterior. No extender meses indefinidamente por el excedente.
- Ejemplo: abono 100, octubre pendiente 100 y pago 110. Mantener noviembre:
  Saldo anterior +10, Saldo -90, Próx. pago noviembre. Pasar a diciembre:
  Saldo anterior -90, Saldo -190, Próx. pago diciembre.
- Ejemplo: octubre pendiente 100 y pago 50. Mantener octubre: Saldo anterior 0,
  Saldo -50, Próx. pago octubre. Pasar a noviembre: Saldo anterior -50,
  Saldo -150, Próx. pago noviembre.
- Un pago habitual exacto genera solamente el próximo débito. Los adelantos
  mantienen exactamente los meses seleccionados, sin un mes adicional fuera
  del período. Si su importe no coincide, se conserva la consulta del adelanto.
- SE VA, precio congelado u omisión explícita de proyección mantienen sus
  restricciones. No ofrecer alquileres nuevos que esas reglas prohíben.
  Sin alquiler previo, se conserva la consulta para emitir el mes del pago.
  Intereses futuros no desplazan los períodos de alquiler.

El modal muestra Próx. pago a la derecha de cada opción, débitos a generar,
Saldo anterior y Saldo final con su signo. El saldo total conserva todos los
movimientos, incluso futuros ya planificados; Saldo anterior se calcula respecto
al mes elegido. La vista previa reproduce el mismo ledger que la confirmación,
incluyendo una sola vez las comisiones, bonificaciones y el recargo real del servidor.

PaymentProjectionPlanner calcula las opciones dentro de la transacción y del
bloqueo por cliente. HTTP 422 PAYMENT_DECISION_REQUIRED revierte todo el intento,
incluyendo pago, movimientos, aumentos y congelamientos. La aprobación liga
FutureDebitAction y PaymentDecisionToken al estado original y a los importes.
Una cuenta modificada se rechaza con HTTP 409; datos u opciones modificados
requieren otra consulta. Finanzas y Dashboard usan el mismo diálogo. Cancelar
conserva el formulario y no publica actualizaciones.

Aplicar Database/Update_Add_PaymentCollectionDecisions.sql antes de desplegar.
La tabla payment_collection_decisions guarda una decisión por pago, dentro de
la misma transacción. Una fila automática con fecha nula reemplaza decisiones
anteriores cuando dejan de corresponder. La última decisión válida tiene prioridad
sobre el cálculo histórico y NO se limita al mes actual: un mes anterior puede
seguir pendiente por decisión expresa. dbo.GetPaymentCollectionMonth y
GetPaymentCollectionPreviousBalance comparten esa lectura entre Clientes,
detalle, filtros, Finanzas, Dashboard, Caja, Comunicaciones y snapshot offline.
Reconstruir saldos no elimina la decisión. Borrar el pago elimina su decisión
por FK y recalcula; si se borra un pago posterior, puede volver a regir la
anterior. Un crédito manual posterior invalida la fecha anterior y recupera el
cálculo por imputaciones; los cambios de importes actualizan los saldos en cada
lectura. Sin una decisión válida, las cuentas históricas conservan su regla.
La migración no altera pagos, importes ni débitos históricos.

Los pagos offline que necesitan consulta quedan fallidos para revisión con
conexión, sin efectos en la cuenta; no decidir por el administrativo al sincronizar.
Database/Check_PaymentProjectionDecisions.sql contiene controles de sólo lectura.
Verificar comportamiento con SQL descartable PaymentDecisionChecks, además de
las pruebas del servicio y diálogo Angular. Compilar no demuestra esta lógica.

## Buscador de Clientes: documentos y contactos (2026-10-09)

El buscador conserva nombre, identificador de pago, DNI y bauleras (incluidas
las compartidas), y busca también por CUIT y cualquiera de los emails activos.
El CUIT admite coincidencia parcial con o sin guiones, espacios o puntos. Los
emails se buscan sin distinguir mayúsculas; los dados de baja no se incluyen.
El filtro SQL usa parámetros y EXISTS para no duplicar clientes ni alterar el
conteo/paginación cuando coinciden varios emails. Conserva el primer email visible.
El modo offline busca sobre dni/cuit/emails del último snapshot actualizado;
los cachés previos sin esos campos siguen cargando y requieren refrescarse con
conexión para buscar esos datos. No hay cambio de versión de IndexedDB.
El tachito «Limpiar búsqueda» se muestra siempre que hay texto, permite teclado
y conserva los demás filtros al vaciar el término. No usar opacidad ligada a
un grupo de hover inexistente. No cambiar saldos ni reglas financieras.
Los cambios de SQL están en los queries del backend: no requieren migración.
Database/Check_ClientSearch.sql permite contrastar coincidencias en sólo lectura.

## Historial de abonos en client-detail (2026-10-09)

La presentación ordena los tramos planificados primero (fecha de inicio más
reciente arriba), luego el activo y después los finalizados/eventos por fecha
descendente. Se conserva el estado recibido del servidor: no reclasificar ni
cambiar vigencias, importes o persistencia por este orden visual.
Cada porcentaje compara el importe del tramo con el tramo cronológicamente
anterior: (nuevo - anterior) / anterior * 100, mostrado con hasta dos decimales.
Los eventos de cambio de medio de pago se mantienen fechados y con los importes
originales, pero no se usan como escalones para duplicar porcentajes.
La referencia es cronológica, independiente del orden priorizado de la vista.
Una reducción se etiqueta Reducción, cero Sin cambios y una base de cero se
informa como «Desde un abono de $0», sin inventar un porcentaje ni dividir por cero.
El tramo inicial no tiene comparación. El contador cuenta tramos, no eventos.
La línea usa segmentos por fila y marcadores sobre el mismo eje; termina en el
primer y último marcador. Edición, eliminación y restricciones de rol conservan
su comportamiento. Este refactor es del frontend y no requiere queries SQL.

## Modal de aumento: Configuración y superficie azul (2026-10-09)

PaymentIncreaseModal se comparte entre Finanzas, Dashboard y la planificación
anticipada desde la ficha del cliente. Al comenzar una etapa sin confirmar,
consulta MonthlyIncrease para el año/mes de ese aumento y usa su porcentaje como
valor inicial editable. La fecha configurada se compara como período calendario,
sin convertirla a la zona horaria del navegador. No tomar el porcentaje del mes
actual, del último registro ni de otro año cuando el período solicitado no existe.
Cada apertura/etapa nueva lee Configuración otra vez; no agregar un caché del valor.

El porcentaje inicial muestra exactamente el de Configuración; el nuevo abono
se calcula con los redondeos ya existentes según el medio de pago. La edición
manual y sus redondeos conservan su funcionamiento. Volver a una etapa confirmada
restaura sus importes/porcentaje y no los reemplaza por el predeterminado.
Cancelar o cambiar de etapa cancela la consulta anterior: una respuesta tardía
no puede modificar el nuevo período. Mientras carga, se desactivan los campos
y la confirmación; siguen disponibles Atrás y Omitir con sus reglas actuales.
Si no existe configuración para ese mes, inicia en cero y lo indica. Si falla
la consulta, conserva los valores actuales, permite ingresarlos manualmente y
ofrece Reintentar. No alterar reglas de anticipos, congelamientos ni omisiones.

La superficie sigue el resumen de costos de create-client-modal: azul sólido
#2563EB, bloques #1D4ED8, campos blancos con texto #123A74. Encabezado simple
«Ajustar abono» y cliente; período y contador de etapas se ubican en el cuerpo.
Mantener el abono proyectado destacado y la fecha del próximo aumento visible.
Sin degradados, sombras decorativas ni badges en el encabezado. En pantallas
pequeñas el cuerpo puede desplazarse y las acciones quedan accesibles.
El cambio es del frontend, usa el endpoint existente y no requiere migración SQL.
Verificar con tsconfig.increase-modal.spec.json, compilación Angular y revisión
visual del componente real con datos simulados; estas pruebas no registran pagos.

