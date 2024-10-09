using Amazon.Lambda.Core;
using cm_lambda_create_contact.Models;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using System.Numerics;
using System.Text.Json;
using Twilio.TwiML.Messaging;
using Newtonsoft.Json;
using BibliotecaChatwoot.Services.Chatwoot;
using BibliotecaChatwoot.Models.Chatwoot;

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
        
        /*Crear contacto*/
        var cw_contact = new BibliotecaChatwoot.Models.Chatwoot.CW_NEW_CONTACT()
        {
            name = input.Nombre,
            phone_number = $"+521{input.Telefono}",
            custom_attributes = new Custom_Attributes()
            {
                campaign_id = input.Campaña_ID,
                cliente="Paciente",
                es_prospecto=true,
                interes_en= input.Campaña_ID,
                recibe_ofertas=true
            }
        };
        var contacts_Service = new CW_Contacts_Service();
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
                Console.WriteLine($"El contacto encontrado fue:{found_contact.payload.id}");
                cw_contact.custom_attributes.campaign_name = "Obtener Videos";
                var cw_updated_contact = contacts_Service.CreateContact(cw_contact, found_contact.payload.id);
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
                    /*Do nothing ya se actualizo*/
                    return StartConversation(cw_updated_contact.payload.contact.id, input);
                }
            }
        }
        else
        {
            /*Se creo con exito en chatwoot*/
            Console.WriteLine("Se pudo crear el contacto de chatwoot en el primer intento 121");
            /*Crearlo en CRM*/
            return StartConversation(cw_new_contact.payload.contact.id, input);
        }        

    }
    private APIGatewayProxyResponse StartConversation(int ContactID, BasicContact input)
    {
        CW_Conversation_Service conversation_Service = new CW_Conversation_Service();
        conversation_Service.EnviarMensajeInicial(ContactID, input.Nombre);
        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = "Conversacion creada"
        };
    }
}
