﻿using Application.Interfaces;
using AutoMapper;
using Core.Entities.InputModels;
using Core.Entities.Models;
using InfraStructure.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Drawing.Imaging;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class EVoucherController : ControllerBase
    {
        private readonly IUnitWork _uniWork;
        private readonly IMapper _mapper;

        public EVoucherController(IUnitWork unitWork, IMapper mapper)
        {
            _uniWork = unitWork;
            _mapper = mapper;
        }

        [HttpPost("CreateEVoucher")]
        public async Task<IActionResult> CreateEVoucherAsync([FromBody] EVoucherModel info)
        {
            try
            {
                bool status = await _uniWork.eVoucher.CreateEVoucherAsync(info);
                if (status)
                {
                    return Ok("Create Successful");
                }
                else return BadRequest("Create Fail");
            }
            catch(Exception ex)
            {
                return BadRequest(new { code = "000", message = ex.Message });
            }
        }

        [HttpPost("UpdateEVoucher")]
        public async Task<IActionResult> UpdateEVoucher([FromBody] Evoucher info)
        {
            try
            {
                bool status = await _uniWork.eVoucher.UpdateEVoucherAsync(info.EVoucherId, info);
                if (status)
                {
                    return Ok("Update Successful");
                }
                else return BadRequest("Update Fail");
            }
            catch (Exception ex)
            {
                return BadRequest(new { code = "000", message = ex.Message });
            }
        }

        [HttpPost("DeleteEVoucher")]
        public async Task<IActionResult> DeleteEVoucher([FromBody] Evoucher info)
        {
            try
            {
                bool status = await _uniWork.eVoucher.DeleteEVoucher(info.EVoucherId);
                if (status)
                {
                    return Ok("Delete Successful");
                }
                else return BadRequest("Delete Fail");
            }
            catch (Exception ex)
            {
                return BadRequest(new { code = "000", message = ex.Message });
            }
        }

        [HttpGet("GetAllEVouchers")]
        public async Task<IActionResult> GetAllEVouchers()
        {
            try
            {
                var result = await _uniWork.eVoucher.GetAllEVouchers();
                if (result == null)
                {
                    return BadRequest("No data found");
                }
                else return Ok(_mapper.Map<IEnumerable<EVoucherModel>>(result));
                //else return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { code = "000", message = ex.Message });
            }
        }

        [HttpGet("GetEVoucher")]
        public async Task<IActionResult> GetEVoucher([FromQuery] string id)
        {
            try
            {
                var result = await _uniWork.eVoucher.GetEVoucherById(id);
                if (result == null)
                {
                    return BadRequest("No data found");
                }
                else return Ok(_mapper.Map<EVoucherModel>(result));
                //else return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { code = "000", message = ex.Message });
            }
        }

        [HttpPost("BuyEVoucher")]
        public async Task<IActionResult> BuyEVoucher([FromBody] BuyEVoucherModel info)
        {
            try
            {
                Evoucher evoucher = await _uniWork.eVoucher.GetEVoucherById(info.EVoucherId);
                if (evoucher != null && evoucher.Quantity != 0)
                {
                    if(evoucher.Amount < info.Quantity)
                        return BadRequest(new { code = "000", message = "Not Enough InStoke" });
                    #region Assign Value
                    Buyevoucher buyevoucher = new Buyevoucher();
                    buyevoucher.BuyTypeId = Guid.NewGuid().ToString();
                    buyevoucher.Phone = info.Phone;
                    buyevoucher.Name = info.Name;
                    buyevoucher.BuyType = info.BuyMethod;
                    buyevoucher.EVoucherId = info.EVoucherId;
                    buyevoucher.Active = true;
                    buyevoucher.CreatedOn = DateTime.Now;

                    Order order = new Order();
                    order.OrderId = Guid.NewGuid().ToString();
                    order.OrderCode = info.OrderCode;
                    order.OrderDate = info.OrderDate;
                    order.PaymentDate = info.PaymentDate;
                    order.EVoucherId = info.EVoucherId;
                    order.TransactionId = info.TransactionId;
                    order.Quantity = info.Quantity;
                    order.Active = true;

                    Core.Entities.Models.Transaction transaction = new Core.Entities.Models.Transaction();
                    transaction.TransactionId = Guid.NewGuid().ToString();
                    transaction.EVoucherId = info.EVoucherId;
                    transaction.Phone = info.Phone;
                    transaction.CreatedOn = DateTime.Now;

                    Promotion promotion = new Promotion();
                    promotion.PromotionId = Guid.NewGuid().ToString();
                    promotion.EVoucherId = buyevoucher.EVoucherId;
                    promotion.CreatedOn = DateTime.Now;
                    #endregion

                    //Caculate amount with discount
                    var discount = info.PaymentMethod == "VISA" ? (int)Discount.VISA : 0;
                    var total = evoucher.Amount * info.Quantity;
                    var payment = await _uniWork.payment.GetPayment(info.PaymentMethod);
                    order.PaymentId = payment == null ? "" : payment.PaymentId;
                    order.TransactionId = transaction.TransactionId;
                    transaction.Amount = total - (total * (double)discount / 100);
                    
                    #region Check BuyType Limit
                    if (buyevoucher.BuyType == "only me usage")
                    {
                        if (order.Quantity > evoucher.MaxEvoucher)
                        {
                            return BadRequest(new { code = "000", message = "You can't buy more than limit!" });
                        }
                    }
                    else
                    {
                        if (order.Quantity > evoucher.MaxGiftEvoucher)
                        {
                            return BadRequest(new { code = "000", message = "You can't buy more than limit!" });
                        }
                    }
                    #endregion
                    if (await _uniWork.buyEVoucher.ModTenCheck(info.CardNumber))
                    {
                        transaction.Status = "Completed";
                        #region PromoCode Generate
                        promotion.PromoCode = await _uniWork.promotion.GeneratePromoCode();
                        #endregion
                        bool status = await _uniWork.buyEVoucher.CreateBuyEVoucherAsync(buyevoucher);//insert Buy Evoucher
                        if (status)
                        {
                            status = await _uniWork.transaction.CreateTransactionAsync(transaction); // insert Transaction
                            if(status) status = await _uniWork.order.CreateOrderAsync(order);// insert Order
                            else return BadRequest(new { code = "000", message = "Your purchase is not successful!" });
                            if (status) status = await _uniWork.promotion.CreatePromotionAsync(promotion); // insert Promotion
                            else return BadRequest(new { code = "000", message = "Your purchase is not successful!" });
                            evoucher.Quantity = evoucher.Quantity - info.Quantity;
                            status = await _uniWork.eVoucher.UpdateEVoucherAsync(info.EVoucherId, evoucher); //Subtract Quantity
                            if(status) return Ok(new { code = "200", message = "Your purchase is completely successful." });
                        }
                        return BadRequest(new { code = "000", message = "Your purchase is not successful!" });
                        
                    }
                    else
                    {
                        transaction.Status = "Fail";
                        await _uniWork.transaction.CreateTransactionAsync(transaction);
                        return BadRequest(new { code = "000", message = "Your purchase is not successful!" });
                    }

                }
                return BadRequest(new { code = "000", message = "This eVoucher is sold out!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { code = "000", message = ex.Message });
            }
        }

        [HttpPost("CreatePayment")]
        public async Task<IActionResult> CreatePaymentAsync([FromBody] PaymentModel info)
        {
            try
            {
                Payment payment = new Payment();
                payment.PaymentId =Guid.NewGuid().ToString();
                payment.PaymentType = info.paymentType;
                payment.Active = true;
                payment.CreatedOn = DateTime.Now;
                bool status = await _uniWork.payment.CreatePaymentAsync(payment);
                if (status)
                {
                    return Ok("Create Successful");
                }
                else return BadRequest("Create Fail");
            }
            catch (Exception ex)
            {
                return BadRequest(new { code = "000", message = ex.Message });
            }
        }
    }
}
