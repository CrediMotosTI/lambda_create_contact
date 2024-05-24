using Amazon.Lambda.Core;
using cm_lambda_create_contact.Models;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using BibliotecasCrediMotos.Services.CRM;
using BibliotecasCrediMotos.Services.Chatwoot;
using BibliotecasCrediMotos.Models.Chatwoot;
using System.Numerics;
using System.Text.Json;
using Twilio.TwiML.Messaging;
using Newtonsoft.Json;
using BibliotecasCrediMotos.Models.CRM;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace cm_lambda_create_contact;

public class Function
{
    
    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest input_request, ILambdaContext context)
    {
        Console.WriteLine($"Body: {input_request.Body}");
        BasicContact input = JsonConvert.DeserializeObject<BasicContact>(input_request.Body);
        Console.WriteLine($"Datos recibidos: {JsonConvert.SerializeObject(input)}");

        if (input == null)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = "Datos vacios"
            };
        }
        if (input.Telefono.Length != 10)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = "Telefono en formato incorrecto"
            };
        }
        /*Revisar si existe el contacto*/
        CRMSesion crm = new CRMSesion();
        if (crm.GetContactByPhoneNumber(input.Telefono))
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = "El telefono ya existe"
            };
        }
        else
        {
            CW_Contacts_Service contacts_Service = new CW_Contacts_Service();
            var cw_contact = new BibliotecasCrediMotos.Models.Chatwoot.CW_NEW_CONTACT()
            {
                name = input.Nombre,
                phone_number = $"+521{input.Telefono}",
                custom_attributes = new Custom_Attributes()
                {
                    campaign_id = input.Campaña_ID,
                    campaign_name ="Obtener Videos",
                    gender = input.Genero,
                    has_ine = false,
                    has_valid_ine = false,
                    has_valid_video = false,
                    has_video = false,
                    ine_url = "",
                    refered_by = input.Referencia,
                    uuid = "",
                    video_url = ""
                }
            };
            var cw_new_contact = contacts_Service.CreateContact(cw_contact);
            if (cw_new_contact == null)
            {
                Console.WriteLine($"No se pudo crear el contacto de chatwoot en el primer intento 84 tel:{input.Telefono}");
                /*Como el contacto ya existe, lo vamos a atualizar*/
                /*Obtener el ID del contacto*/
                var found_contact = contacts_Service.SearchContact(input.Telefono);

                if (found_contact == null)
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = 400,
                        Body = "Ocurrio un error en chatwoot 92"
                    };
                }
                else
                {
                    /*Ya tengo el ID del usuario de chatwoot*/
                    Console.WriteLine($"El contacto encontrado fue:{found_contact.payload[0].id}");
                    cw_contact.custom_attributes.campaign_name = "Obtener Videos";
                    var cw_updated_contact = contacts_Service.CreateContact(cw_contact, found_contact.payload[0].id);
                    if (cw_updated_contact == null)
                    {
                        return new APIGatewayProxyResponse
                        {
                            StatusCode = 400,
                            Body = "Ocurrio un error en chatwoot 104"
                        };
                    }
                    else
                    {
                        /*Se actualizo en chatwoot*/
                        /*Crearlo en CRM*/
                        return CreateCRMContact(crm, cw_updated_contact, input);
                    }
                }
            }
            else
            {
                /*Se creo con exito en chatwoot*/
                Console.WriteLine("Se pudo crear el contacto de chatwoot en el primer intento 121");
                /*Crearlo en CRM*/
                return CreateCRMContact(crm, cw_new_contact, input);
            }
        }

    }
    private APIGatewayProxyResponse CreateCRMContact(CRMSesion crm, CW_Created_Contact NewContact, BasicContact input)
    {
        Console.WriteLine($"Contacto a crear: {JsonConvert.SerializeObject(NewContact)}");
        var crm_contact = crm.CreateContact(NewContact);
        if (!crm_contact)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = "Error desconocido"
            };
        }
        else
        {
            /*Contacto creado exitosamente, falta iniciar la conversacion*/
            CW_Conversation_Service conversation_Service = new CW_Conversation_Service();
            conversation_Service.EnviarMensajeInicial(NewContact.payload.contact.id, input.Nombre);
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = "Conversacion creada"
            };
        }
    }
}
