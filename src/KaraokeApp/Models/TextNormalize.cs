using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

public class TextNormalizer
{
    public static string NormalizeText(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // 1. Remover caracteres especiais (acentos, cedilha, etc.)
        string normalized = RemoveSpecialCharacters(input);
        
        // 2. Converter para minúsculas
        normalized = normalized.ToLower();
        
        // 3. Substituir espaços por hífens
        normalized = normalized.Replace(" ", "-");
        
        return normalized;
    }

    private static string RemoveSpecialCharacters(string text)
    {
        // Remover acentos e caracteres especiais
        string normalized = text.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder();
        
        foreach (char c in normalized)
        {
            UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}