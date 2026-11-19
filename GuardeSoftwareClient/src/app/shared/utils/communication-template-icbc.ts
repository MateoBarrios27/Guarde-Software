// Contenido original de las plantillas ICBC del sistema (antes dentro de communications.component.ts).
// El catálogo de plantillas del sistema está en communication-template-catalog.ts.

export const ICBC_TEMPLATE_CONTENT = `
<p style="color: #111827;"><strong>Estimado/a: {data[0]}</strong></p>
<p style="color: #1d4ed8;"><strong>POR SER CLIENTE DE GUARDE LO QUE QUIERA</strong></p>
<p style="color: #15803d;"><strong>EL BANCO ICBC LE OFRECE BONIFICACIONES EN CUENTAS, PAQUETES Y MUCHO MÁS.</strong></p>
<p style="color: #b91c1c;"><strong>CONTACTO ICBC:</strong> Natalia Pedro 113478-9917</p>
<p style="color: #6b7280;">Saludos</p>
<p style="color: #6b7280;">La Administración</p>
<p><a href="https://www.guardeloquequiera.net/">guardeloquequiera.net</a></p>
<p style="color: #15803d;">WhatsApp 115-780-0251</p>`;

export const ICBC_2_TEMPLATE_CONTENT = `
<!doctype html>
<html lang="es">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="x-apple-disable-message-reformatting">
    <title>Beneficios ICBC para nuestros clientes</title>
</head>

<body style="
    margin: 0;
    padding: 0;
    background-color: #f3f5f7;
    font-family: Arial, Helvetica, sans-serif;
    color: #263238;
">

    <!-- Texto de previsualización que aparece junto al asunto -->
    <div style="
        display: none;
        max-height: 0;
        overflow: hidden;
        opacity: 0;
        color: transparent;
        mso-hide: all;
    ">
        Consultá las bonificaciones disponibles en cuentas, paquetes bancarios
        y otras propuestas de ICBC.
    </div>

    <table
        role="presentation"
        width="100%"
        cellpadding="0"
        cellspacing="0"
        border="0"
        style="
            width: 100%;
            background-color: #f3f5f7;
            border-collapse: collapse;
        "
    >
        <tr>
            <td align="center" style="padding: 32px 12px;">

                <table
                    role="presentation"
                    width="100%"
                    cellpadding="0"
                    cellspacing="0"
                    border="0"
                    style="
                        width: 100%;
                        max-width: 620px;
                        background-color: #ffffff;
                        border-collapse: separate;
                        border-spacing: 0;
                        border-radius: 14px;
                        overflow: hidden;
                        box-shadow: 0 6px 22px rgba(25, 45, 60, 0.10);
                    "
                >

                    <!-- Encabezado textual -->
                    <tr>
                        <td
                            style="
                                padding: 25px 32px;
                                background-color: #17324d;
                                border-bottom: 4px solid #d71920;
                            "
                        >
                            <p style="
                                margin: 0 0 7px;
                                color: #ffffff;
                                font-size: 20px;
                                line-height: 26px;
                                font-weight: 700;
                                letter-spacing: 0.4px;
                            ">
                                GUARDE LO QUE QUIERA
                            </p>

                            <p style="
                                margin: 0;
                                color: #cbd8e3;
                                font-size: 12px;
                                line-height: 18px;
                                font-weight: 600;
                                letter-spacing: 1px;
                                text-transform: uppercase;
                            ">
                                Beneficio especial para clientes
                            </p>
                        </td>
                    </tr>

                    <!-- Presentación principal -->
                    <tr>
                        <td style="padding: 36px 32px 18px;">

                            <p style="
                                margin: 0 0 12px;
                                color: #d71920;
                                font-size: 13px;
                                line-height: 18px;
                                font-weight: 700;
                                letter-spacing: 0.9px;
                                text-transform: uppercase;
                            ">
                                Propuesta especial ICBC
                            </p>

                            <h1 style="
                                margin: 0 0 22px;
                                color: #17324d;
                                font-size: 30px;
                                line-height: 38px;
                                font-weight: 700;
                            ">
                                Más beneficios por ser nuestro cliente
                            </h1>

                            <p style="
                                margin: 0 0 18px;
                                color: #263238;
                                font-size: 17px;
                                line-height: 27px;
                            ">
                                Hola, <strong>{data[0]}</strong>:
                            </p>

                            <p style="
                                margin: 0;
                                color: #455a64;
                                font-size: 16px;
                                line-height: 26px;
                            ">
                                Por ser cliente de
                                <strong style="color: #263238;">
                                    Guarde Lo Que Quiera
                                </strong>,
                                queremos acercarte una propuesta especial de
                                <strong style="color: #263238;">ICBC</strong>
                                con bonificaciones y alternativas pensadas para vos.
                            </p>

                        </td>
                    </tr>

                    <!-- Beneficios -->
                    <tr>
                        <td style="padding: 14px 32px 26px;">

                            <table
                                role="presentation"
                                width="100%"
                                cellpadding="0"
                                cellspacing="0"
                                border="0"
                                style="
                                    width: 100%;
                                    background-color: #f7f9fb;
                                    border: 1px solid #e2e8ed;
                                    border-radius: 10px;
                                "
                            >
                                <tr>
                                    <td style="padding: 24px;">

                                        <p style="
                                            margin: 0 0 16px;
                                            color: #17324d;
                                            font-size: 17px;
                                            line-height: 24px;
                                            font-weight: 700;
                                        ">
                                            Podés consultar por:
                                        </p>

                                        <table
                                            role="presentation"
                                            width="100%"
                                            cellpadding="0"
                                            cellspacing="0"
                                            border="0"
                                        >
                                            <tr>
                                                <td
                                                    width="25"
                                                    valign="top"
                                                    style="
                                                        padding: 3px 0 11px;
                                                        color: #d71920;
                                                        font-size: 17px;
                                                        font-weight: 700;
                                                    "
                                                >
                                                    ✓
                                                </td>
                                                <td style="
                                                    padding: 0 0 11px;
                                                    color: #455a64;
                                                    font-size: 15px;
                                                    line-height: 23px;
                                                ">
                                                    Bonificaciones en cuentas.
                                                </td>
                                            </tr>

                                            <tr>
                                                <td
                                                    width="25"
                                                    valign="top"
                                                    style="
                                                        padding: 3px 0 11px;
                                                        color: #d71920;
                                                        font-size: 17px;
                                                        font-weight: 700;
                                                    "
                                                >
                                                    ✓
                                                </td>
                                                <td style="
                                                    padding: 0 0 11px;
                                                    color: #455a64;
                                                    font-size: 15px;
                                                    line-height: 23px;
                                                ">
                                                    Beneficios en paquetes bancarios.
                                                </td>
                                            </tr>

                                            <tr>
                                                <td
                                                    width="25"
                                                    valign="top"
                                                    style="
                                                        padding: 3px 0 0;
                                                        color: #d71920;
                                                        font-size: 17px;
                                                        font-weight: 700;
                                                    "
                                                >
                                                    ✓
                                                </td>
                                                <td style="
                                                    padding: 0;
                                                    color: #455a64;
                                                    font-size: 15px;
                                                    line-height: 23px;
                                                ">
                                                    Otras alternativas disponibles según tu
                                                    perfil y las condiciones vigentes.
                                                </td>
                                            </tr>
                                        </table>

                                    </td>
                                </tr>
                            </table>

                        </td>
                    </tr>

                    <!-- Contacto -->
                    <tr>
                        <td style="padding: 0 32px 34px;">

                            <p style="
                                margin: 0 0 18px;
                                color: #263238;
                                font-size: 18px;
                                line-height: 26px;
                                font-weight: 700;
                            ">
                                ¿Querés conocer más?
                            </p>

                            <p style="
                                margin: 0 0 20px;
                                color: #455a64;
                                font-size: 15px;
                                line-height: 24px;
                            ">
                                Para recibir asesoramiento y conocer las opciones,
                                los requisitos y las condiciones vigentes,
                                comunicate directamente con:
                            </p>

                            <table
                                role="presentation"
                                width="100%"
                                cellpadding="0"
                                cellspacing="0"
                                border="0"
                                style="
                                    width: 100%;
                                    background-color: #fff5f5;
                                    border-left: 4px solid #d71920;
                                    border-radius: 8px;
                                "
                            >
                                <tr>
                                    <td style="padding: 19px 20px;">

                                        <p style="
                                            margin: 0 0 4px;
                                            color: #263238;
                                            font-size: 17px;
                                            line-height: 24px;
                                            font-weight: 700;
                                        ">
                                            Natalia Pedro
                                        </p>

                                        <p style="
                                            margin: 0 0 7px;
                                            color: #607d8b;
                                            font-size: 14px;
                                            line-height: 21px;
                                        ">
                                            Contacto ICBC
                                        </p>

                                        <p style="
                                            margin: 0;
                                            color: #263238;
                                            font-size: 17px;
                                            line-height: 24px;
                                            font-weight: 700;
                                        ">
                                            <a
                                                href="tel:+541134789917"
                                                style="
                                                    color: #d71920;
                                                    text-decoration: none;
                                                "
                                            >
                                                11 3478-9917
                                            </a>
                                        </p>

                                    </td>
                                </tr>
                            </table>

                            <!-- Botón principal -->
                            <table
                                role="presentation"
                                cellpadding="0"
                                cellspacing="0"
                                border="0"
                                align="center"
                                style="margin: 26px auto 0;"
                            >
                                <tr>
                                    <td
                                        align="center"
                                        bgcolor="#d71920"
                                        style="
                                            border-radius: 7px;
                                            background-color: #d71920;
                                        "
                                    >
                                        <a
                                            href="tel:+541134789917"
                                            style="
                                                display: inline-block;
                                                padding: 14px 28px;
                                                color: #ffffff;
                                                font-size: 15px;
                                                line-height: 20px;
                                                font-weight: 700;
                                                text-decoration: none;
                                                border-radius: 7px;
                                            "
                                        >
                                            Consultar beneficios
                                        </a>
                                    </td>
                                </tr>
                            </table>

                        </td>
                    </tr>

                    <!-- Despedida -->
                    <tr>
                        <td
                            style="
                                padding: 27px 32px;
                                background-color: #f7f9fb;
                                border-top: 1px solid #e2e8ed;
                            "
                        >
                            <p style="
                                margin: 0 0 7px;
                                color: #455a64;
                                font-size: 15px;
                                line-height: 23px;
                            ">
                                Esperamos que esta propuesta te resulte útil.
                            </p>

                            <p style="
                                margin: 0;
                                color: #263238;
                                font-size: 15px;
                                line-height: 23px;
                            ">
                                Saludos,<br>
                                <strong>La Administración</strong><br>
                                Guarde Lo Que Quiera
                            </p>
                        </td>
                    </tr>

                    <!-- Datos de Guarde Lo Que Quiera -->
                    <tr>
                        <td
                            align="center"
                            style="
                                padding: 25px 26px;
                                background-color: #17324d;
                            "
                        >
                            <p style="
                                margin: 0 0 9px;
                                color: #ffffff;
                                font-size: 14px;
                                line-height: 22px;
                                font-weight: 700;
                            ">
                                Guarde Lo Que Quiera
                            </p>

                            <p style="
                                margin: 0 0 8px;
                                color: #d9e3ea;
                                font-size: 13px;
                                line-height: 21px;
                            ">
                                WhatsApp:
                                <a
                                    href="https://wa.me/5491157800251"
                                    target="_blank"
                                    style="
                                        color: #83d9a5;
                                        font-weight: 700;
                                        text-decoration: none;
                                    "
                                >
                                    11 5780-0251
                                </a>
                            </p>

                            <p style="
                                margin: 0;
                                color: #d9e3ea;
                                font-size: 13px;
                                line-height: 21px;
                            ">
                                <a
                                    href="https://www.guardeloquequiera.net/"
                                    target="_blank"
                                    style="
                                        color: #ffffff;
                                        font-weight: 700;
                                        text-decoration: underline;
                                    "
                                >
                                    www.guardeloquequiera.net
                                </a>
                            </p>
                        </td>
                    </tr>

                </table>

                <!-- Aclaración comercial -->
                <table
                    role="presentation"
                    width="100%"
                    cellpadding="0"
                    cellspacing="0"
                    border="0"
                    style="width: 100%; max-width: 620px;"
                >
                    <tr>
                        <td align="center" style="padding: 18px 20px 0;">

                            <p style="
                                margin: 0;
                                color: #7b8a92;
                                font-size: 11px;
                                line-height: 17px;
                            ">
                                Esta comunicación tiene carácter informativo.
                                Las bonificaciones, los productos y sus condiciones
                                comerciales dependen de ICBC y pueden variar.
                                Consultá las condiciones vigentes con el contacto indicado.
                            </p>

                        </td>
                    </tr>
                </table>

            </td>
        </tr>
    </table>

</body>
</html>
`;
