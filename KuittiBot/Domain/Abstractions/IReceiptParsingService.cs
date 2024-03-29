﻿using KuittiBot.Functions.Domain.Models;
using KuittiBot.Functions.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Domain.Abstractions
{
    public interface IReceiptParsingService
    {
        Receipt ParseProductsFromReceiptPdf(Stream stream);
        Task<Receipt> ParseProductsFromReceiptImageAsync(Stream stream);
        Task<Receipt> ParseProductsFromReceiptImageAsync_old(Stream stream);

    }
}
