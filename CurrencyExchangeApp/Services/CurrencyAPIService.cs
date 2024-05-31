using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using CurrencyExchangeApp.Models;
using Newtonsoft.Json;

namespace CurrencyExchangeApp.Services
{
    public class CurrencyAPIService
    {
        private readonly HttpClient _httpClient;
        private const string BaseApiUrl = "https://api.nbrb.by/";

        public CurrencyAPIService()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(BaseApiUrl) };
        }

        public async Task<List<Rate>> GetCurrencies(DateTime? onDate = null, int periodicity = 0)
        {
            try
            {
                string dateParameter = onDate.HasValue ? $"?ondate={onDate.Value:yyyy-MM-dd}" : string.Empty;
                string response = await SendGetRequestAsync($"exrates/rates?{dateParameter}&periodicity={periodicity}");

                List<Rate> currencies = JsonConvert.DeserializeObject<List<Rate>>(response);

                return currencies;
            }
            catch (HttpRequestException ex)
            {
                HandleRequestException(ex);
            }
            catch (JsonException ex)
            {
                HandleJsonException(ex);
            }
            catch (Exception ex)
            {
                HandleOtherException(ex);
            }

            return null;
        }

        public async Task<List<RateShort>> GetCurrencyDynamics(int curId, DateTime startDate, DateTime endDate)
        {
            try
            {
                string response = await SendGetRequestAsync($"exrates/rates/dynamics/{curId}?startdate={startDate:yyyy-MM-dd}&enddate={endDate:yyyy-MM-dd}");

                List<RateShort> dynamics = JsonConvert.DeserializeObject<List<RateShort>>(response);

                return dynamics;
            }
            catch (HttpRequestException ex)
            {
                HandleRequestException(ex);
            }
            catch (JsonException ex)
            {
                HandleJsonException(ex);
            }
            catch (Exception ex)
            {
                HandleOtherException(ex);
            }

            return null;
        }

        private async Task<string> SendGetRequestAsync(string relativeUrl)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(relativeUrl);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                HandleRequestException(ex);
            }
            catch (Exception ex)
            {
                HandleOtherException(ex);
            }

            return null;
        }

        private void HandleRequestException(HttpRequestException ex)
        {
            throw new Exception("Ошибка HTTP-запроса: " + ex.Message, ex);
        }

        private void HandleJsonException(JsonException ex)
        {
            throw new Exception("Ошибка десериализации JSON: " + ex.Message, ex);
        }

        private void HandleOtherException(Exception ex)
        {
            throw new Exception("Произошла ошибка: " + ex.Message, ex);
        }
    }
}
