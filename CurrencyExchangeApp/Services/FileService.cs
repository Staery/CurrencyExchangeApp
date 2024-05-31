using CurrencyExchangeApp.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

public class FileService
{
    private readonly string _filePath;

    public FileService(string filePath)
    {
        _filePath = filePath;
    }

    public List<Rate> ReadDataFromFile()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                string json = File.ReadAllText(_filePath);
                return DeserializeJson<List<Rate>>(json);
            }
            else
            {
                return new List<Rate>();
            }
        }
        catch (Exception ex)
        {
            HandleFileException("Ошибка чтения файла", ex);
        }

        return null;
    }

    public void WriteDataToFile(List<Rate> data)
    {
        try
        {
            string json = SerializeToJson(data);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            HandleFileException("Ошибка записи файла", ex);
        }
    }

    public void UpdateDataInFile(List<Rate> newData)
    {
        WriteDataToFile(newData);
    }

    private T DeserializeJson<T>(string json)
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (JsonException ex)
        {
            HandleFileException("Ошибка десериализации JSON", ex);
        }

        return default;
    }

    private string SerializeToJson(object obj)
    {
        try
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }
        catch (JsonException ex)
        {
            HandleFileException("Ошибка сериализации JSON", ex);
        }

        return null;
    }

    private void HandleFileException(string errorMessage, Exception ex)
    {
        throw new Exception(errorMessage + ": " + ex.Message, ex);
    }
}