using NUnit.Framework;
using StockSimulator.Models;
using System;
using System.Collections.Generic;

namespace StockSimulatorTest
{
    public class Tests
    {
        IEnumerable<Stock> testData = new List<Stock>();

        [SetUp]
        public void Setup()
        {
            


        }

        [Test]
        public void Test1()
        {
            Assert.Pass();
        }

        [Test]
        public void TestGetUniqueSymbols()
        {
            IEnumerable<Stock> symbols = new List<Stock>();

            DateTime date = DateTime.Parse("1997-09-19");

        
        }

        [Test]
        public void TestGetPriceDataAndCombineWithInput()
        {
            
        }

        [Test]
        public void TestGetNetworthDataPoints()
        {

        }

        [Test]
        public void TestCalculate()
        {

        }
    }
}